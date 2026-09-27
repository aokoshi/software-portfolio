<?php
declare(strict_types=1);
require __DIR__.'/../src/Inventory.php';
header('Content-Type: application/json; charset=utf-8');header('Cache-Control: no-store');header('X-Content-Type-Options: nosniff');
ini_set('session.use_strict_mode','1');ini_set('session.use_only_cookies','1');
$sessionDir=__DIR__.'/../storage/sessions';if(!is_dir($sessionDir))mkdir($sessionDir,0700,true);session_save_path($sessionDir);session_name(config()['database']==='stockflow'?'stockflow_session':'stockflow_'.substr(hash('sha256',config()['database']),0,12));
session_set_cookie_params(['httponly'=>true,'samesite'=>'Strict','secure'=>!empty($_SERVER['HTTPS'])&&$_SERVER['HTTPS']!=='off','path'=>'/']);session_start();
$_SESSION['csrf']??=bin2hex(random_bytes(32));
function respond(array $body,int $status=200): never {http_response_code($status);echo json_encode($body,JSON_UNESCAPED_UNICODE|JSON_INVALID_UTF8_SUBSTITUTE);exit;}
try {
    $db=connect();$action=$_GET['action']??'state';$method=$_SERVER['REQUEST_METHOD'];
    if(!in_array($method,['GET','POST'],true))throw new AppError('Метод не поддерживается.',405);
    $user=null;if(isset($_SESSION['user_id'])&&($_SESSION['expires']??0)>time())$user=query($db,'SELECT id,name,email,role FROM users WHERE id=?',[$_SESSION['user_id']])->fetch()?:null;
    if($action==='session'&&$method==='GET')respond(['user'=>$user,'csrf'=>$_SESSION['csrf'],'needsSetup'=>(int)query($db,'SELECT COUNT(*) FROM users')->fetchColumn()===0]);
    $data=[];
    if($method==='POST'){
        if(!hash_equals($_SESSION['csrf'],$_SERVER['HTTP_X_CSRF_TOKEN']??''))throw new AppError('Обновите страницу и повторите запрос.',403);
        if(!str_starts_with($_SERVER['CONTENT_TYPE']??'','application/json'))throw new AppError('Ожидается JSON.',415);
        $raw=file_get_contents('php://input',false,null,0,65537);if(strlen($raw)>65536)throw new AppError('Слишком большой запрос.',413);
        $data=json_decode($raw,true,512,JSON_THROW_ON_ERROR);if(!is_array($data)||array_is_list($data)&&count($data))throw new AppError('Некорректные данные.');
    }
    if(in_array($action,['login','setup'],true)&&$method==='POST') {
        $bucket=hash('sha256',$_SERVER['REMOTE_ADDR']??'local');
        $allowed=atomic($db,function()use($db,$bucket){
            query($db,'INSERT IGNORE INTO login_limits(bucket,attempts,reset_at) VALUES (?,0,?)',[$bucket,time()+900]);
            $row=query($db,'SELECT * FROM login_limits WHERE bucket=? FOR UPDATE',[$bucket])->fetch();
            $attempts=(int)$row['reset_at']<time()?1:(int)$row['attempts']+1;$reset=(int)$row['reset_at']<time()?time()+900:(int)$row['reset_at'];
            query($db,'UPDATE login_limits SET attempts=?,reset_at=? WHERE bucket=?',[$attempts,$reset,$bucket]);return $attempts<=20;
        });
        if(!$allowed)throw new AppError('Слишком много попыток входа. Повторите через 15 минут.',429);
        $email=strtolower(strval_checked($data['email']??null,'Email',5,160));if(!filter_var($email,FILTER_VALIDATE_EMAIL))throw new AppError('Введите корректный email.');
        $password=$data['password']??'';if(!is_string($password)||strlen($password)<10||strlen($password)>72)throw new AppError('Пароль: от 10 до 72 байт.');
        if($action==='setup'){
            if(!query($db,"SELECT GET_LOCK('stockflow_setup_".config()['database']."',5)")->fetchColumn())throw new AppError('Повторите запрос.',409);
            try{
                if((int)query($db,'SELECT COUNT(*) FROM users')->fetchColumn()>0)throw new AppError('Администратор уже создан. Войдите в аккаунт.',409);
                $name=strval_checked($data['name']??null,'Имя',2,80);
                query($db,"INSERT INTO users(name,email,password_hash,role) VALUES (?,?,?,'admin')",[$name,$email,password_hash($password,PASSWORD_DEFAULT)]);
                $id=(int)$db->lastInsertId();audit($db,$id,'setup','Создан первый администратор');
            }finally{query($db,"SELECT RELEASE_LOCK('stockflow_setup_".config()['database']."')");}
            $user=query($db,'SELECT id,name,email,role FROM users WHERE id=?',[$id])->fetch();
        }else{
            $row=query($db,'SELECT * FROM users WHERE email=?',[$email])->fetch();
            $hash=$row['password_hash']??'$2y$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2uheWG/igi.';
            if(!password_verify($password,$hash)||!$row)throw new AppError('Неверный email или пароль.',401);
            $user=array_intersect_key($row,array_flip(['id','name','email','role']));
        }
        session_regenerate_id(true);$_SESSION['user_id']=$user['id'];$_SESSION['expires']=time()+28800;$_SESSION['csrf']=bin2hex(random_bytes(32));respond(['user'=>$user,'csrf'=>$_SESSION['csrf']]);
    }
    if($action==='logout'&&$method==='POST'){$_SESSION=[];session_destroy();setcookie(session_name(),'',['expires'=>1,'path'=>'/','httponly'=>true,'samesite'=>'Strict']);respond(['ok'=>true]);}
    if(!$user)throw new AppError('Войдите в аккаунт.',401);
    session_write_close();
    if($action==='state'&&$method==='GET') {
        $products=query($db,'SELECT p.*,COALESCE(SUM(s.quantity),0) quantity,COALESCE(SUM(s.reserved),0) reserved FROM products p LEFT JOIN stocks s ON s.product_id=p.id GROUP BY p.id ORDER BY p.id DESC')->fetchAll();
        $warehouses=query($db,'SELECT * FROM warehouses ORDER BY id')->fetchAll();$stocks=query($db,'SELECT * FROM stocks')->fetchAll();
        $orders=query($db,'SELECT o.*,w.name warehouse,u.name author FROM orders o JOIN warehouses w ON w.id=o.warehouse_id JOIN users u ON u.id=o.actor_id ORDER BY o.id DESC')->fetchAll();
        $items=query($db,'SELECT * FROM order_items ORDER BY order_id,product_id')->fetchAll();$byOrder=[];foreach($items as $item)$byOrder[$item['order_id']][]=$item;foreach($orders as &$order)$order['items']=$byOrder[$order['id']]??[];unset($order);
        respond(['products'=>$products,'warehouses'=>$warehouses,'stocks'=>$stocks,'orders'=>$orders,'user'=>$user]);
    }
    if($action==='movements'&&$method==='GET') {
        $where=[];$params=[];
        foreach(['from'=>'>=','to'=>'<='] as $key=>$comparison){
            if(!empty($_GET[$key])){$value=$_GET[$key];$date=DateTimeImmutable::createFromFormat('!Y-m-d',$value);if(!$date||$date->format('Y-m-d')!==$value)throw new AppError('Некорректная дата.');$where[]="m.created_at $comparison ?";$params[]=$value.($key==='from'?' 00:00:00':' 23:59:59');}
        }
        if(!empty($_GET['from'])&&!empty($_GET['to'])&&$_GET['from']>$_GET['to'])throw new AppError('Начало периода позже окончания.');
        if(!empty($_GET['warehouse'])){$where[]='m.warehouse_id=?';$params[]=int_checked((int)$_GET['warehouse'],'Склад');}
        if(!empty($_GET['kind'])){$kind=$_GET['kind'];if(!in_array($kind,['receipt','writeoff','transfer_in','transfer_out','reserve','release','ship'],true))throw new AppError('Неизвестная операция.');$where[]='m.kind=?';$params[]=$kind;}
        $filter=$where?' WHERE '.implode(' AND ',$where):'';
        $count=(int)query($db,'SELECT COUNT(*) FROM movements m'.$filter,$params)->fetchColumn();
        $page=max(1,min(100000,(int)($_GET['page']??1)));$offset=($page-1)*50;
        $movements=query($db,'SELECT m.*,p.name product,p.sku,w.name warehouse,COALESCE(u.name,\'Система\') author FROM movements m JOIN products p ON p.id=m.product_id JOIN warehouses w ON w.id=m.warehouse_id LEFT JOIN users u ON u.id=m.actor_id'.$filter." ORDER BY m.id DESC LIMIT 50 OFFSET $offset",$params)->fetchAll();
        $totals=query($db,"SELECT COALESCE(SUM(CASE WHEN kind='receipt' THEN delta ELSE 0 END),0) receipts, COALESCE(SUM(CASE WHEN kind='ship' THEN -delta ELSE 0 END),0) shipped, COALESCE(SUM(CASE WHEN kind='writeoff' THEN -delta ELSE 0 END),0) written_off FROM movements m".$filter,$params)->fetch();
        respond(['movements'=>$movements,'total'=>$count,'page'=>$page,'pages'=>(int)ceil($count/50),'totals'=>$totals]);
    }
    if($action==='audit'&&$method==='GET') {
        role($user,['admin']);respond(['events'=>query($db,'SELECT a.*,u.name author FROM audit a LEFT JOIN users u ON u.id=a.actor_id ORDER BY a.id DESC LIMIT 200')->fetchAll()]);
    }
    if($action==='users'&&$method==='GET'){role($user,['admin']);respond(['users'=>query($db,'SELECT id,name,email,role,created_at FROM users ORDER BY id')->fetchAll()]);}
    if($action==='users'&&$method==='POST') {
        role($user,['admin']);$name=strval_checked($data['name']??null,'Имя',2,80);$email=strtolower(strval_checked($data['email']??null,'Email',5,160));
        if(!filter_var($email,FILTER_VALIDATE_EMAIL))throw new AppError('Введите корректный email.');if(!in_array($data['role']??'', ['admin','manager','warehouse'],true))throw new AppError('Выберите роль.');
        $password=$data['password']??'';if(!is_string($password)||strlen($password)<10||strlen($password)>72)throw new AppError('Пароль: от 10 до 72 байт.');
        $id=atomic($db,function()use($db,$name,$email,$password,$data,$user){query($db,'INSERT INTO users(name,email,password_hash,role) VALUES (?,?,?,?)',[$name,$email,password_hash($password,PASSWORD_DEFAULT),$data['role']]);$id=(int)$db->lastInsertId();audit($db,(int)$user['id'],'user_created',"$name · {$data['role']}");return $id;});respond(['id'=>$id],201);
    }
    if($method==='POST')respond((new Inventory($db))->command($user,$action,$data));
    throw new AppError('Адрес не найден.',404);
}catch(AppError $e){respond(['error'=>$e->getMessage()],$e->status);}
catch(JsonException){respond(['error'=>'Некорректный JSON.'],400);}
catch(PDOException $e){error_log($e->getMessage());if(($e->errorInfo[1]??0)===1062)respond(['error'=>'Артикул, email или название уже используются.'],409);if(in_array($e->errorInfo[1]??0,[1213,1205],true))respond(['error'=>'Другая операция обновляет эти данные. Повторите запрос.'],409);respond(['error'=>'База недоступна. Запустите MySQL в XAMPP и выполните scripts/install.php.'],503);}
catch(Throwable $e){error_log($e->getMessage());respond(['error'=>'Ошибка сервера. Повторите запрос.'],500);}
