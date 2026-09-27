export const categories=[
{id:'food',name:'Еда и продукты',icon:'restaurant-outline',color:'#DE9A59'},
{id:'transport',name:'Транспорт',icon:'bus-outline',color:'#6888C8'},
{id:'home',name:'Дом',icon:'home-outline',color:'#9A80C8'},
{id:'study',name:'Учёба',icon:'book-outline',color:'#4F9B91'},
{id:'fun',name:'Отдых',icon:'game-controller-outline',color:'#CE839C'},
{id:'health',name:'Здоровье',icon:'heart-outline',color:'#D47A72'},
{id:'shopping',name:'Покупки',icon:'bag-outline',color:'#A9A059'},
{id:'other',name:'Другое',icon:'ellipse-outline',color:'#84909D'}
];
export const incomeCategories=[{id:'salary',name:'Зарплата'},{id:'stipend',name:'Стипендия'},{id:'gift',name:'Подарок'},{id:'other-income',name:'Другой доход'}];
export const uid=()=>Date.now().toString(36)+'-'+Math.random().toString(36).slice(2,10);
export const today=()=>{const d=new Date();return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')};
export function validDate(value){if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value))return false;const [y,m,d]=value.split('-').map(Number);const dt=new Date(y,m-1,d);return y>=2000&&y<=2100&&dt.getFullYear()===y&&dt.getMonth()===m-1&&dt.getDate()===d;}
export function money(value){return (value/100).toLocaleString('ru-RU',{maximumFractionDigits:2})+' ₸'}
export function parseMoney(value){const s=String(value).trim().replace(/\s/g,'').replace(',','.');if(!/^\d{1,9}(\.\d{1,2})?$/.test(s))throw new Error('Введи положительную сумму, не больше двух знаков после запятой.');const [whole,fraction='']=s.split('.');const result=Number(whole)*100+Number(fraction.padEnd(2,'0'));if(!Number.isSafeInteger(result)||result<=0||result>100_000_000_000)throw new Error('Сумма должна быть больше нуля и не выше 1 млрд ₸.');return result;}
export const empty=()=>({app:'PocketBudget',version:1,initialized:false,demo:false,transactions:[],budgets:[],goals:[]});
export function monthShift(month,delta){const [y,m]=month.split('-').map(Number);const dt=new Date(y,m-1+delta,1);return dt.getFullYear()+'-'+String(dt.getMonth()+1).padStart(2,'0')}
export const monthName=month=>new Date(month+'-15T12:00:00').toLocaleDateString('ru-RU',{month:'long',year:'numeric'});
export function summary(transactions,month){const rows=transactions.filter(t=>t.date.startsWith(month));let income=0,expense=0;const byCategory={};for(const t of rows){if(t.type==='income')income+=t.amount;else{expense+=t.amount;byCategory[t.category]=(byCategory[t.category]??0)+t.amount}}return {income,expense,net:income-expense,byCategory,rows};}
function text(v,max=100){return typeof v==='string'&&v.trim().length>0&&v.length<=max}
function amount(v,zero=false){return Number.isSafeInteger(v)&&v>=(zero?0:1)&&v<=100_000_000_000}
function ids(rows){return rows.every(r=>r&&text(r.id)&&!Array.isArray(r))&&new Set(rows.map(r=>r.id)).size===rows.length}
export function validate(v){
 if(!v||v.app!=='PocketBudget'||v.version!==1||typeof v.initialized!=='boolean'||typeof v.demo!=='boolean')throw new Error('Это не резервная копия PocketBudget версии 1.');
 if(!['transactions','budgets','goals'].every(k=>Array.isArray(v[k])&&v[k].length<=10000&&ids(v[k])))throw new Error('Некорректный список записей.');
 for(const t of v.transactions)if(!validDate(t.date)||!amount(t.amount)||!['income','expense'].includes(t.type)||!(t.type==='income'?incomeCategories:categories).some(c=>c.id===t.category)||typeof t.note!=='string'||t.note.length>500)throw new Error('Проверь дату, сумму и категорию операции.');
 for(const b of v.budgets)if(!/^\d{4}-(0[1-9]|1[0-2])$/.test(b.month)||!categories.some(c=>c.id===b.category)||!amount(b.limit))throw new Error('Некорректный бюджет.');
 if(new Set(v.budgets.map(b=>b.month+':'+b.category)).size!==v.budgets.length)throw new Error('Повторный бюджет для одной категории.');
 for(const g of v.goals)if(!text(g.name,80)||!amount(g.target)||!amount(g.saved,true))throw new Error('Некорректная цель накопления.');
 return v;
}
export function demo(){
 const d=today(),month=d.slice(0,7);
 return {...empty(),initialized:true,demo:true,transactions:[
 {id:'d1',type:'income',amount:9000000,category:'stipend',date:month+'-01',note:'Стипендия'},
 {id:'d2',type:'income',amount:4500000,category:'salary',date:month+'-02',note:'Небольшой проект'},
 {id:'d3',type:'expense',amount:1250000,category:'food',date:d,note:'Продукты на неделю'},
 {id:'d4',type:'expense',amount:350000,category:'transport',date:d,note:'Проездной'},
 {id:'d5',type:'expense',amount:790000,category:'study',date:d,note:'Курс по TypeScript'},
 {id:'d6',type:'expense',amount:420000,category:'fun',date:d,note:'Вечер с друзьями'}
 ],budgets:[{id:'b1',month,category:'food',limit:4000000},{id:'b2',month,category:'transport',limit:1000000},{id:'b3',month,category:'fun',limit:1500000}],goals:[{id:'g1',name:'Поездка летом',target:15000000,saved:4500000}]};
}
export function csv(rows){const escape=value=>'"'+String(value).replace(/^[=+@-]/,match=>"'"+match).replaceAll('"','""')+'"';return '\uFEFF'+[['Дата','Тип','Категория','Сумма KZT','Комментарий'],...rows.map(t=>[t.date,t.type==='income'?'Доход':'Расход',[...categories,...incomeCategories].find(c=>c.id===t.category)?.name??t.category,(t.amount/100).toFixed(2),t.note])].map(row=>row.map(escape).join(';')).join('\r\n')}
