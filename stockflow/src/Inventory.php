<?php
declare(strict_types=1);
require_once __DIR__.'/bootstrap.php';
final class Inventory {
    public function __construct(private PDO $db) {}
    // All stock rows are locked in product/warehouse order to avoid inverted lock order.
    private function lockStock(int $product,int $warehouse): array {
        query($this->db,'INSERT INTO stocks(product_id,warehouse_id) VALUES (?,?) ON DUPLICATE KEY UPDATE product_id=VALUES(product_id)',[$product,$warehouse]);
        return query($this->db,'SELECT * FROM stocks WHERE product_id=? AND warehouse_id=? FOR UPDATE',[$product,$warehouse])->fetch();
    }
    private function exists(string $table,int $id): array {
        $row=query($this->db,"SELECT * FROM $table WHERE id=?",[$id])->fetch();
        if(!$row)throw new AppError('Товар, склад или заказ не найден.',404);return $row;
    }
    private function move(int $product,int $warehouse,string $kind,int $delta,int $reservedDelta,int $actor,string $reason,string $reference,?int $order=null): void {
        $row=query($this->db,'SELECT * FROM stocks WHERE product_id=? AND warehouse_id=? FOR UPDATE',[$product,$warehouse])->fetch();
        $quantity=(int)$row['quantity']+$delta;$reserved=(int)$row['reserved']+$reservedDelta;
        if($quantity<0 || $reserved<0 || $reserved>$quantity) throw new AppError('Недостаточно свободного остатка. Зарезервированный товар недоступен.',409);
        if($quantity>100000000) throw new AppError('Превышен допустимый остаток.',409);
        query($this->db,'UPDATE stocks SET quantity=?,reserved=? WHERE product_id=? AND warehouse_id=?',[$quantity,$reserved,$product,$warehouse]);
        query($this->db,'INSERT INTO movements(product_id,warehouse_id,kind,delta,reserved_delta,balance,reserved_balance,actor_id,order_id,reason,reference) VALUES (?,?,?,?,?,?,?,?,?,?,?)',[$product,$warehouse,$kind,$delta,$reservedDelta,$quantity,$reserved,$actor,$order,$reason,$reference]);
    }
    public function command(array $user,string $action,array $data): array {
        $permissions=['product'=>['admin','manager'],'warehouse'=>['admin'],'receipt'=>['admin','warehouse'],'writeoff'=>['admin','warehouse'],'transfer'=>['admin','warehouse'],'order'=>['admin','manager'],'ship'=>['admin','warehouse'],'cancel'=>['admin','manager']];
        if(!isset($permissions[$action]))throw new AppError('Неизвестная операция.',404);
        role($user,$permissions[$action]);
        $key=strval_checked($data['requestKey']??null,'Ключ запроса',16,80);
        $hash=hash('sha256',$action.json_encode($data,JSON_UNESCAPED_UNICODE));
        return atomic($this->db,function()use($user,$action,$data,$key,$hash){
            try{query($this->db,'INSERT INTO commands(request_key,actor_id,payload_hash) VALUES (?,?,?)',[$key,$user['id'],$hash]);}
            catch(PDOException $e){
                if(($e->errorInfo[1]??0)!==1062)throw $e;
                $existing=query($this->db,'SELECT * FROM commands WHERE request_key=? FOR UPDATE',[$key])->fetch();
                if((int)$existing['actor_id']!==(int)$user['id']||$existing['payload_hash']!==$hash)throw new AppError('Ключ запроса уже использован.',409);
                return json_decode($existing['result_json'],true);
            }
            $result=$this->perform($user,$action,$data);
            query($this->db,'UPDATE commands SET result_json=? WHERE request_key=?',[json_encode($result),$key]);return $result;
        });
    }
    private function perform(array $user,string $action,array $data): array {
        $actor=(int)$user['id'];$reference=uuid();
        if($action==='product') {
            $sku=strtoupper(strval_checked($data['sku']??null,'Артикул',2,40));
            if(!preg_match('/^[A-Z0-9_-]+$/',$sku))throw new AppError('Артикул: латинские буквы, цифры, дефис и подчёркивание.');
            $name=strval_checked($data['name']??null,'Название',3,120);
            $params=[$sku,$name,strval_checked($data['category']??null,'Категория',2,60),strval_checked($data['supplier']??null,'Поставщик',2,100),int_checked($data['cost']??null,'Закупочная цена',0,1000000000),int_checked($data['price']??null,'Цена продажи',1,1000000000),int_checked($data['minimum']??null,'Минимум',0,1000000)];
            if(isset($data['id'])){
                $id=int_checked($data['id'],'Товар');$this->exists('products',$id);
                query($this->db,'UPDATE products SET sku=?,name=?,category=?,supplier=?,cost=?,price=?,minimum=? WHERE id=?',[...$params,$id]);
                audit($this->db,$actor,'product_updated',"$sku · $name");
            }else{query($this->db,'INSERT INTO products(sku,name,category,supplier,cost,price,minimum) VALUES (?,?,?,?,?,?,?)',$params);$id=(int)$this->db->lastInsertId();audit($this->db,$actor,'product_created',"$sku · $name");}
            return ['id'=>$id];
        }
        if($action==='warehouse'){
            $name=strval_checked($data['name']??null,'Название склада',2,100);$address=strval_checked($data['address']??'','Адрес',0,200);
            query($this->db,'INSERT INTO warehouses(name,address) VALUES (?,?)',[$name,$address]);$id=(int)$this->db->lastInsertId();audit($this->db,$actor,'warehouse_created',$name);return ['id'=>$id];
        }
        if(in_array($action,['receipt','writeoff','transfer'],true)) {
            $product=int_checked($data['product_id']??null,'Товар');$warehouse=int_checked($data['warehouse_id']??null,'Склад');$quantity=int_checked($data['quantity']??null,'Количество');
            $this->exists('products',$product);$this->exists('warehouses',$warehouse);$reason=strval_checked($data['reason']??null,'Основание',3,1000);
            if($action==='transfer'){
                $target=int_checked($data['target_id']??null,'Склад назначения');if($target===$warehouse)throw new AppError('Выберите разные склады.');$this->exists('warehouses',$target);
                $ids=[$warehouse,$target];sort($ids,SORT_NUMERIC);foreach($ids as $id)$this->lockStock($product,$id);
                $this->move($product,$warehouse,'transfer_out',-$quantity,0,$actor,$reason,$reference);
                $this->move($product,$target,'transfer_in',$quantity,0,$actor,$reason,$reference);
            }else{$this->lockStock($product,$warehouse);$this->move($product,$warehouse,$action,$action==='receipt'?$quantity:-$quantity,0,$actor,$reason,$reference);}
            audit($this->db,$actor,$action,"Товар #$product · $quantity шт. · $reason");return ['reference'=>$reference];
        }
        if($action==='order') {
            $warehouse=int_checked($data['warehouse_id']??null,'Склад');$this->exists('warehouses',$warehouse);
            $customer=strval_checked($data['customer']??null,'Клиент',2,120);$note=strval_checked($data['note']??'','Комментарий',0,1000);
            $items=$data['items']??null;if(!is_array($items)||!count($items)||count($items)>30)throw new AppError('Заказ должен содержать от 1 до 30 позиций.');
            $quantities=[];
            foreach($items as $item){if(!is_array($item))throw new AppError('Некорректная позиция.');$id=int_checked($item['product_id']??null,'Товар');$q=int_checked($item['quantity']??null,'Количество');if(isset($quantities[$id]))throw new AppError('Товар повторяется в заказе.');$quantities[$id]=$q;}
            ksort($quantities,SORT_NUMERIC);
            query($this->db,'INSERT INTO orders(customer,warehouse_id,actor_id,note) VALUES (?,?,?,?)',[$customer,$warehouse,$actor,$note]);$order=(int)$this->db->lastInsertId();
            foreach($quantities as $id=>$q){$product=$this->exists('products',$id);$this->lockStock($id,$warehouse);$this->move($id,$warehouse,'reserve',0,$q,$actor,"Резерв по заказу #$order",$reference,$order);query($this->db,'INSERT INTO order_items(order_id,product_id,title,sku,quantity,price) VALUES (?,?,?,?,?,?)',[$order,$id,$product['name'],$product['sku'],$q,$product['price']]);}
            audit($this->db,$actor,'order_reserved',"Заказ #$order · $customer");return ['id'=>$order];
        }
        if(in_array($action,['ship','cancel'],true)) {
            $id=int_checked($data['id']??null,'Заказ');$order=query($this->db,'SELECT * FROM orders WHERE id=? FOR UPDATE',[$id])->fetch();
            if(!$order)throw new AppError('Заказ не найден.',404);if($order['status']!=='reserved')throw new AppError('Заказ уже завершён или отменён.',409);
            $items=query($this->db,'SELECT * FROM order_items WHERE order_id=? ORDER BY product_id',[$id])->fetchAll();
            foreach($items as $item){$p=(int)$item['product_id'];$w=(int)$order['warehouse_id'];$q=(int)$item['quantity'];$this->lockStock($p,$w);$this->move($p,$w,$action==='ship'?'ship':'release',$action==='ship'?-$q:0,-$q,$actor,($action==='ship'?'Отгрузка':'Отмена')." заказа #$id",$reference,$id);}
            $status=$action==='ship'?'shipped':'cancelled';query($this->db,'UPDATE orders SET status=? WHERE id=?',[$status,$id]);audit($this->db,$actor,'order_'.$status,"Заказ #$id");return ['id'=>$id,'status'=>$status];
        }
        throw new AppError('Неизвестная операция.');
    }
}
