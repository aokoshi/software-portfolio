import {useEffect,useRef,useState} from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import {Platform} from 'react-native';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import * as DocumentPicker from 'expo-document-picker';
export function useStore<T>(key:string,empty:()=>T,validate:(value:any)=>any){
 const [data,setData]=useState<T>(empty),[ready,setReady]=useState(false),[error,setError]=useState(''),[warning,setWarning]=useState(''),[busy,setBusy]=useState(false);
 const current=useRef<T>(data),queue=useRef<Promise<any>>(Promise.resolve()),primaryValid=useRef(false);
 async function load(){setReady(false);setError('');try{
 const text=await AsyncStorage.getItem(key);
 if(text===null){current.current=empty();setData(current.current);setReady(true);return;}
 try{const parsed=JSON.parse(text);validate(parsed);current.current=parsed;primaryValid.current=true;}
 catch{
 const backup=await AsyncStorage.getItem(key+'.backup');if(!backup)throw new Error('Сохранение повреждено. Импортируй резервную копию или явно начни заново.');
 const parsed=JSON.parse(backup);validate(parsed);current.current=parsed;primaryValid.current=false;setWarning('Восстановлена предыдущая корректная копия.');
 }
 setData(current.current);setReady(true);
 }catch(e:any){setError(e.message??'Не удалось прочитать данные');}}
 useEffect(()=>{void load()},[]);
 function update(change:(old:T)=>T):Promise<void>{
 const job=queue.current.catch(()=>{}).then(async()=>{setBusy(true);try{
 const next=change(current.current);validate(next);const serialized=JSON.stringify(next);
 if(primaryValid.current)await AsyncStorage.setItem(key+'.backup',JSON.stringify(current.current));
 await AsyncStorage.setItem(key,serialized);primaryValid.current=true;current.current=next;setData(next);setReady(true);setError('');
 }finally{setBusy(false)}});
 queue.current=job;return job;
 }
 return {data,ready,error,warning,busy,update,reload:load};
}
export async function exportFile(name:string,contents:string,mime='application/json'){
 if(Platform.OS==='web'){const blob=new Blob([contents],{type:mime});const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);return;}
 if(!FileSystem.cacheDirectory)throw new Error('Не удалось открыть папку для экспорта.');
 const path=FileSystem.cacheDirectory+name;await FileSystem.writeAsStringAsync(path,contents);
 if(!await Sharing.isAvailableAsync())throw new Error('Отправка файлов недоступна на этом устройстве.');
 await Sharing.shareAsync(path,{mimeType:mime,dialogTitle:'Сохранить копию'});
}
export async function importFile(){
 const result=await DocumentPicker.getDocumentAsync({type:['application/json','text/plain'],copyToCacheDirectory:true,multiple:false});
 if(result.canceled)return null;
 const asset=result.assets[0];if(asset.size&&asset.size>2_000_000)throw new Error('Файл слишком большой. Максимум 2 МБ.');
 const text=Platform.OS==='web'&&asset.file?await asset.file.text():await FileSystem.readAsStringAsync(asset.uri);
 if(text.length>2_000_000)throw new Error('Файл слишком большой.');
 try{return JSON.parse(text)}catch{throw new Error('Нужен JSON-файл резервной копии этого приложения.')}
}
