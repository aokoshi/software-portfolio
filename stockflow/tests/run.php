<?php
declare(strict_types=1);
require __DIR__.'/../src/Inventory.php';
$dbName='stockflow_test_'.bin2hex(random_bytes(5));putenv('STOCKFLOW_DB_NAME='.$dbName);
if(config()['database']!==$dbName)throw new RuntimeException('Tests require database name from STOCKFLOW_DB_NAME; remove database override from config.local.php.');
$root=connect(false);$root->exec("CREATE DATABASE `$dbName` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");$db=connect();
$sql=file_get_contents(__DIR__.'/../database/schema.sql');$db->exec(preg_replace('/^\xEF\xBB\xBF/','',$sql));
$inventory=new Inventory($db);$checks=0;
function check(bool $condition,string $label): void {global $checks;if(!$condition)throw new RuntimeException('FAIL: '.$label);$checks++;echo 'PASS '.$label."\n";}
function rejected(callable $fn,int $status,string $label): void {try{$fn();throw new RuntimeException('Expected failure: '.$label);}catch(AppError $e){check($e->status===$status,$label);}}
function cmd(array $user,string $action,array $data): array {global $inventory;return $inventory->command($user,$action,['requestKey'=>uuid(),...$data]);}
try{
 $actors=[];foreach(['admin','manager','warehouse'] as $r){query($db,'INSERT INTO users(name,email,password_hash,role) VALUES (?,?,?,?)',[$r,$r.'@test.invalid',password_hash('Test-only-password',PASSWORD_DEFAULT),$r]);$actors[$r]=['id'=>(int)$db->lastInsertId(),'role'=>$r];}
 $admin=$actors['admin'];$manager=$actors['manager'];$staff=$actors['warehouse'];
 $a=cmd($admin,'warehouse',['name'=>'Warehouse A','address'=>'Test'])['id'];$b=cmd($admin,'warehouse',['name'=>'Warehouse B','address'=>'Test'])['id'];
 $productData=['sku'=>'TEST-001','name'=>'Test product','category'=>'Components','supplier'=>'Test supplier','cost'=>100,'price'=>200,'minimum'=>1];
 $p=cmd($manager,'product',$productData)['id'];$p2=cmd($admin,'product',[...$productData,'sku'=>'TEST-002'])['id'];
 rejected(fn()=>cmd($manager,'receipt',['product_id'=>$p,'warehouse_id'=>$a,'quantity'=>10,'reason'=>'Test receipt']),403,'Manager cannot modify physical stock');
 rejected(fn()=>cmd($staff,'product',[...$productData,'sku'=>'DENIED']),403,'Warehouse role cannot edit prices');
 $receipt=['requestKey'=>uuid(),'product_id'=>$p,'warehouse_id'=>$a,'quantity'=>10,'reason'=>'Initial receipt'];
 $first=$inventory->command($staff,'receipt',$receipt);$second=$inventory->command($staff,'receipt',$receipt);
 check($first===$second,'Duplicate operation returns original result');
 check((int)query($db,'SELECT quantity FROM stocks WHERE product_id=? AND warehouse_id=?',[$p,$a])->fetchColumn()===10,'Duplicate receipt does not increase balance');
 rejected(fn()=>$inventory->command($staff,'receipt',[...$receipt,'quantity'=>20]),409,'Idempotency key cannot be reused for a different quantity');
 $order=cmd($manager,'order',['warehouse_id'=>$a,'customer'=>'Test customer','items'=>[['product_id'=>$p,'quantity'=>8]]])['id'];
 $row=query($db,'SELECT * FROM stocks WHERE product_id=? AND warehouse_id=?',[$p,$a])->fetch();check((int)$row['quantity']===10&&(int)$row['reserved']===8,'Reservation preserves physical stock');
 rejected(fn()=>cmd($staff,'writeoff',['product_id'=>$p,'warehouse_id'=>$a,'quantity'=>3,'reason'=>'Must fail']),409,'Reserved stock cannot be written off');
 rejected(fn()=>cmd($staff,'transfer',['product_id'=>$p,'warehouse_id'=>$a,'target_id'=>$b,'quantity'=>3,'reason'=>'Must fail']),409,'Reserved stock cannot be transferred');
 cmd($staff,'transfer',['product_id'=>$p,'warehouse_id'=>$a,'target_id'=>$b,'quantity'=>2,'reason'=>'Transfer free stock']);
 check((int)query($db,'SELECT SUM(quantity) FROM stocks WHERE product_id=?',[$p])->fetchColumn()===10,'Transfer preserves total physical stock');
 rejected(fn()=>cmd($staff,'ship',['id'=>999999]),404,'Missing order rejected');
 cmd($staff,'ship',['id'=>$order]);$row=query($db,'SELECT * FROM stocks WHERE product_id=? AND warehouse_id=?',[$p,$a])->fetch();check((int)$row['quantity']===0&&(int)$row['reserved']===0,'Shipping reduces physical and reserved balances');
 rejected(fn()=>cmd($staff,'ship',['id'=>$order]),409,'Completed order cannot ship twice');
 $cancel=cmd($manager,'order',['warehouse_id'=>$b,'customer'=>'Cancel me','items'=>[['product_id'=>$p,'quantity'=>1]]])['id'];cmd($manager,'cancel',['id'=>$cancel]);
 $row=query($db,'SELECT * FROM stocks WHERE product_id=? AND warehouse_id=?',[$p,$b])->fetch();check((int)$row['quantity']===2&&(int)$row['reserved']===0,'Cancellation only releases reservation');
 $count=(int)query($db,'SELECT COUNT(*) FROM orders')->fetchColumn();
 rejected(fn()=>cmd($manager,'order',['warehouse_id'=>$b,'customer'=>'Atomic order','items'=>[['product_id'=>$p,'quantity'=>1],['product_id'=>$p2,'quantity'=>1]]]),409,'Multi-item order fails if any item is unavailable');
 check((int)query($db,'SELECT COUNT(*) FROM orders')->fetchColumn()===$count,'Failed order leaves no order record');
 check((int)query($db,'SELECT reserved FROM stocks WHERE product_id=? AND warehouse_id=?',[$p,$b])->fetchColumn()===0,'Failed order rolls back earlier item reservation');
 cmd($manager,'product',[...$productData,'id'=>$p,'price'=>999]);
 check((int)query($db,'SELECT price FROM order_items WHERE order_id=?',[$order])->fetchColumn()===200,'Order keeps its original price');
 cmd($staff,'receipt',['product_id'=>$p2,'warehouse_id'=>$a,'quantity'=>1,'reason'=>'Last unit for race']);
 $start=microtime(true)+0.4;$workers=[];
 for($i=0;$i<2;$i++){$pipes=[];$process=proc_open([PHP_BINARY,__DIR__.'/race-worker.php',$dbName,(string)$manager['id'],(string)$p2,(string)$a,(string)$start],[0=>['pipe','r'],1=>['pipe','w'],2=>['pipe','w']],$pipes);fclose($pipes[0]);$workers[]=[$process,$pipes];}
 $results=[];foreach($workers as [$process,$pipes]){$out=stream_get_contents($pipes[1]);$err=stream_get_contents($pipes[2]);fclose($pipes[1]);fclose($pipes[2]);$exit=proc_close($process);if($exit!==0)throw new RuntimeException($err);$results[]=json_decode($out,true,512,JSON_THROW_ON_ERROR);}
 check(count(array_filter($results,fn($r)=>$r['ok']))===1,'Two concurrent orders: exactly one reserves the last unit');
 check(count(array_filter($results,fn($r)=>!$r['ok']&&$r['status']===409))===1,'Second concurrent buyer receives a stock conflict');
 foreach(query($db,'SELECT * FROM stocks')->fetchAll() as $s){$sum=query($db,'SELECT COALESCE(SUM(delta),0) q,COALESCE(SUM(reserved_delta),0) r FROM movements WHERE product_id=? AND warehouse_id=?',[$s['product_id'],$s['warehouse_id']])->fetch();check((int)$sum['q']===(int)$s['quantity']&&(int)$sum['r']===(int)$s['reserved'],'Ledger reconciles for product '.$s['product_id'].' warehouse '.$s['warehouse_id']);}
 $newConnection=connect();check((int)query($newConnection,'SELECT COUNT(*) FROM products')->fetchColumn()===2,'Data persists across connections');$newConnection=null;
 echo "All $checks checks passed.\n";
}finally{
 $inventory=null;$db=null;
 // This randomly named database was created by this test run; never target a user database.
 if(!preg_match('/^stockflow_test_[a-f0-9]{10}$/',$dbName))throw new RuntimeException('Unsafe cleanup target');
 $root->exec("DROP DATABASE `$dbName`");
}
