<?php
declare(strict_types=1);
function config(): array {
    $base = require __DIR__.'/../config.example.php';
    return is_file(__DIR__.'/../config.local.php') ? array_merge($base,require __DIR__.'/../config.local.php') : $base;
}
function connect(bool $withDatabase=true): PDO {
    $c=config();
    if (!preg_match('/^[a-zA-Z0-9_]+$/',$c['database'])) throw new RuntimeException('Invalid database name');
    $dsn="mysql:host={$c['host']};port={$c['port']};charset=utf8mb4".($withDatabase?";dbname={$c['database']}":'');
    $db=new PDO($dsn,$c['username'],$c['password'],[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION,PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC,PDO::ATTR_EMULATE_PREPARES=>false]);
    $db->exec("SET time_zone = '+00:00'");
    return $db;
}
function query(PDO $db,string $sql,array $params=[]): PDOStatement {
    $statement=$db->prepare($sql);$statement->execute($params);return $statement;
}
final class AppError extends RuntimeException {
    public function __construct(string $message,public int $status=400) { parent::__construct($message); }
}
function strval_checked(mixed $value,string $label,int $min,int $max): string {
    if (!is_string($value) || mb_strlen(trim($value))<$min || mb_strlen(trim($value))>$max) throw new AppError("$label: от $min до $max символов.");
    return trim($value);
}
function int_checked(mixed $value,string $label,int $min=1,int $max=1000000): int {
    if (!is_int($value) || $value<$min || $value>$max) throw new AppError("$label: целое число от $min до $max.");
    return $value;
}
function role(array $user,array $allowed): void {
    if (!in_array($user['role'],$allowed,true)) throw new AppError('У вашей роли нет доступа к этому действию.',403);
}
function uuid(): string {
    $hex=bin2hex(random_bytes(16));return substr($hex,0,8).'-'.substr($hex,8,4).'-'.substr($hex,12,4).'-'.substr($hex,16,4).'-'.substr($hex,20);
}
function audit(PDO $db,int $actor,string $action,string $detail): void {
    query($db,'INSERT INTO audit(actor_id,action,detail) VALUES (?,?,?)',[$actor,$action,$detail]);
}
function atomic(PDO $db,callable $fn): mixed {
    $db->beginTransaction();try {$result=$fn();$db->commit();return $result;}catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}
