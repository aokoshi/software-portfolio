<?php
declare(strict_types=1);
require __DIR__.'/../src/bootstrap.php';
try {
    $db=connect(false);$name=config()['database'];
    $db->exec("CREATE DATABASE IF NOT EXISTS `$name` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
    $db=connect();$sql=file_get_contents(__DIR__.'/../database/schema.sql');$sql=preg_replace('/^\xEF\xBB\xBF/','',$sql);$db->exec($sql);
    if(!query($db,'SELECT COUNT(*) FROM warehouses')->fetchColumn()) {
        atomic($db,function()use($db){
            query($db,'INSERT INTO warehouses(name,address) VALUES (?,?),(?,?)',['Основной склад','Астана · зона A','Пункт выдачи','Астана · зона B']);
        });
    }
    if(in_array('--demo',$argv,true) && !query($db,'SELECT COUNT(*) FROM products')->fetchColumn()) {
        atomic($db,function()use($db){
            $rows=[['CPU-001','AMD Ryzen 5 7600','Процессоры','Digital Supply',79000,99000,5,18],['GPU-001','GeForce RTX 4060 8GB','Видеокарты','Tech Distribution',145000,179000,4,3],['RAM-001','Kingston Fury 32GB DDR5','Оперативная память','Digital Supply',34000,42900,8,32],['SSD-001','Samsung 990 EVO 1TB','Накопители','Storage Partner',39000,49900,6,24],['MB-001','MSI PRO B650M-A','Материнские платы','Tech Distribution',65000,79900,3,12],['PSU-001','DeepCool 750W Gold','Блоки питания','Digital Supply',29000,37900,4,2],['CASE-001','Montech AIR 100','Корпуса','Tech Distribution',24000,31900,3,9],['COOL-001','DeepCool AK400','Охлаждение','Digital Supply',10500,14900,5,16]];
            $warehouse=(int)query($db,'SELECT MIN(id) FROM warehouses')->fetchColumn();
            foreach($rows as $r){$quantity=array_pop($r);query($db,'INSERT INTO products(sku,name,category,supplier,cost,price,minimum) VALUES (?,?,?,?,?,?,?)',$r);$id=(int)$db->lastInsertId();query($db,'INSERT INTO stocks(product_id,warehouse_id,quantity) VALUES (?,?,?)',[$id,$warehouse,$quantity]);query($db,"INSERT INTO movements(product_id,warehouse_id,kind,delta,balance,reserved_balance,reason,reference) VALUES (?,?,'receipt',?,?,0,?,?)",[$id,$warehouse,$quantity,$quantity,'Демонстрационное поступление',uuid()]);}
        });
    }
    echo "StockFlow database is ready. Open the site to create the first administrator.\n";
}catch(Throwable $e){fwrite(STDERR,"Installation failed: ".$e->getMessage()."\n");exit(1);}
