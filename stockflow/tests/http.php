<?php
// HTTP smoke checks against an explicitly supplied disposable test instance.
declare(strict_types=1);
$base=$argv[1]??'';$email=getenv('STOCKFLOW_TEST_EMAIL');$password=getenv('STOCKFLOW_TEST_PASSWORD');
if(!preg_match('~^http://127\.0\.0\.1:\d+$~',$base)||!$email||!$password)exit("Usage: php tests/http.php http://127.0.0.1:TEST_PORT; set STOCKFLOW_TEST_EMAIL and STOCKFLOW_TEST_PASSWORD.\n");
$cookie='';$csrf='';$checks=0;
function request(string $action,?array $data=null,bool $token=true): array {
 global $base,$cookie,$csrf;
 $headers="Accept: application/json\r\nCookie: $cookie\r\n";
 if($data!==null){$headers.="Content-Type: application/json\r\n";if($token)$headers.="X-CSRF-Token: $csrf\r\n";}
 $ctx=stream_context_create(['http'=>['method'=>$data===null?'GET':'POST','header'=>$headers,'content'=>$data===null?'':json_encode($data),'ignore_errors'=>true,'timeout'=>10]]);
 $body=file_get_contents($base.'/api.php?action='.urlencode($action),false,$ctx);
 foreach($http_response_header as $line)if(preg_match('/^Set-Cookie: ([^;]+)/i',$line,$m))$cookie=$m[1];
 preg_match('/\s(\d{3})\s/',$http_response_header[0],$m);return [(int)$m[1],json_decode($body,true)];
}
function expect(bool $ok,string $label):void{global $checks;if(!$ok)throw new RuntimeException('FAIL '.$label);$checks++;echo "PASS $label\n";}
[$status,$session]=request('session');$csrf=$session['csrf'];expect($status===200&&!$session['user'],'Anonymous session');
expect(request('state')[0]===401,'Private data requires login');
expect(request('login',['email'=>$email,'password'=>$password],false)[0]===403,'Missing CSRF rejected');
expect(request('login',['email'=>$email,'password'=>'Definitely-wrong-password'])[0]===401,'Wrong password rejected');
[$status,$login]=request('login',['email'=>$email,'password'=>$password]);expect($status===200&&isset($login['user']),'Login works');$csrf=$login['csrf'];
expect(request('state')[0]===200,'Authenticated data available');
expect(request('setup',['name'=>'Second Admin','email'=>'duplicate@test.invalid','password'=>'Test-password-2026'])[0]===409,'Second initial administrator rejected');
expect(request('logout',[])[0]===200,'Logout works');
expect(request('state')[0]===401,'Session invalid after logout');
echo "$checks HTTP checks passed.\n";
