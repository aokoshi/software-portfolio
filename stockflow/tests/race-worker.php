<?php
declare(strict_types=1);
require __DIR__.'/../src/Inventory.php';
if(!preg_match('/^stockflow_test_[a-z0-9_]+$/',$argv[1]??''))exit(2);
putenv('STOCKFLOW_DB_NAME='.$argv[1]);$db=connect();$inventory=new Inventory($db);
while(microtime(true)<(float)$argv[5])usleep(1000);
try{$result=$inventory->command(['id'=>(int)$argv[2],'role'=>'manager'],'order',['requestKey'=>uuid(),'customer'=>'Concurrent client','warehouse_id'=>(int)$argv[4],'items'=>[['product_id'=>(int)$argv[3],'quantity'=>1]]]);echo json_encode(['ok'=>true,'result'=>$result]);}
catch(AppError $e){echo json_encode(['ok'=>false,'status'=>$e->status]);}
