import {spawn} from 'node:child_process';import {networkInterfaces} from 'node:os';
const addresses=Object.values(networkInterfaces()).flat().filter(n=>n&&n.family==='IPv4'&&!n.internal).map(n=>n.address);
const host=addresses.find(ip=>ip.startsWith('192.168.'))??addresses.find(ip=>/^(10\.|172\.(1[6-9]|2\d|3[01])\.)/.test(ip));
const env={...process.env,...(host?{REACT_NATIVE_PACKAGER_HOSTNAME:host}:{}),EXPO_NO_TELEMETRY:'1'};
const args=['node_modules/expo/bin/cli','start','--go','--lan','--port',process.argv[2]||'8081'];
if(host)console.log('iPhone on the same Wi-Fi: exp://'+host+':'+(process.argv[2]||'8081'));
const child=spawn(process.execPath,args,{stdio:'inherit',env});
child.on('exit',code=>process.exit(code??0));
process.on('SIGINT',()=>child.kill('SIGINT'));
