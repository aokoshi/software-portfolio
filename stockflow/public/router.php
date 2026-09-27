<?php
// PHP built-in server router. Only the public directory is exposed.
$path=parse_url($_SERVER['REQUEST_URI'],PHP_URL_PATH);
if($path==='/'||$path==='/index.html'){readfile(__DIR__.'/index.html');return true;}
if(in_array($path,['/app.js','/styles.css','/favicon.svg'],true))return false;
if($path==='/api.php'){require __DIR__.'/api.php';return true;}
http_response_code(404);echo 'Not found';
