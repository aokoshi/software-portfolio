export const uid=()=>Date.now().toString(36)+'-'+Math.random().toString(36).slice(2,10);
export const days=['Пн','Вт','Ср','Чт','Пт','Сб','Вс'];
export const colors=['#625ADD','#328B85','#D68B48','#C0668B','#648BC4','#929B4B'];
export const today=()=>{const d=new Date();return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')};
export function validDate(value){if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value))return false;const [y,m,d]=value.split('-').map(Number);const dt=new Date(y,m-1,d);return y>=2000&&y<=2100&&dt.getFullYear()===y&&dt.getMonth()===m-1&&dt.getDate()===d;}
export function minutes(time){if(typeof time!=='string'||!/^([01]\d|2[0-3]):[0-5]\d$/.test(time))return NaN;const[h,m]=time.split(':').map(Number);return h*60+m;}
export const weekday=()=>((new Date().getDay()+6)%7)+1;
export function lessonsOn(lessons,day){return lessons.filter(l=>l.day===day).sort((a,b)=>a.start.localeCompare(b.start))}
export function clashes(lessons,lesson){return lessons.some(l=>l.id!==lesson.id&&l.day===lesson.day&&minutes(l.start)<minutes(lesson.end)&&minutes(lesson.start)<minutes(l.end))}
export function taskStatus(task,date=today()){return task.done?'done':task.due<date?'overdue':task.due===date?'today':'upcoming'}
export function gradeAverage(grades){const total=grades.reduce((s,g)=>s+g.weight,0);return total?grades.reduce((s,g)=>s+g.score/g.max*100*g.weight,0)/total:null;}
export const empty=()=>({app:'CampusMate',version:1,initialized:false,demo:false,courses:[],lessons:[],tasks:[],grades:[]});
function text(v,max=100){return typeof v==='string'&&v.trim().length>0&&v.length<=max}
function ids(rows){return rows.every(r=>r&&text(r.id)&&!Array.isArray(r))&&new Set(rows.map(r=>r.id)).size===rows.length}
export function validate(v){
 if(!v||v.app!=='CampusMate'||v.version!==1||typeof v.initialized!=='boolean'||typeof v.demo!=='boolean')throw new Error('Это не резервная копия CampusMate версии 1.');
 if(!['courses','lessons','tasks','grades'].every(k=>Array.isArray(v[k])&&v[k].length<=10000&&ids(v[k])))throw new Error('Некорректный список записей.');
 for(const c of v.courses)if(!text(c.name,80)||typeof c.teacher!=='string'||c.teacher.length>100||!colors.includes(c.color))throw new Error('Проверь название предмета.');
 for(const key of ['lessons','tasks','grades'])for(const item of v[key])if(!v.courses.some(c=>c.id===item.courseId))throw new Error('Запись ссылается на неизвестный предмет.');
 for(const l of v.lessons)if(!Number.isInteger(l.day)||l.day<1||l.day>7||!Number.isFinite(minutes(l.start))||!Number.isFinite(minutes(l.end))||minutes(l.end)<=minutes(l.start)||typeof l.room!=='string'||l.room.length>100||!['Лекция','Практика','Лабораторная'].includes(l.kind))throw new Error('Проверь время занятия: формат ЧЧ:ММ, окончание позже начала.');
 for(const t of v.tasks)if(!text(t.title,100)||!validDate(t.due)||typeof t.done!=='boolean'||!['Обычный','Высокий'].includes(t.priority)||typeof t.note!=='string'||t.note.length>500)throw new Error('Проверь задание и дату в формате ГГГГ-ММ-ДД.');
 for(const g of v.grades)if(!text(g.title,100)||![g.score,g.max,g.weight].every(Number.isFinite)||g.max<=0||g.max>1000||g.score<0||g.score>g.max||g.weight<=0||g.weight>100)throw new Error('Балл должен быть от 0 до максимума; вес — больше 0 и не выше 100.');
 return v;
}
export function removeCourse(v,id){return {...v,courses:v.courses.filter(c=>c.id!==id),lessons:v.lessons.filter(c=>c.courseId!==id),tasks:v.tasks.filter(c=>c.courseId!==id),grades:v.grades.filter(c=>c.courseId!==id)}}
export function demo(){
 const next=new Date();next.setDate(next.getDate()+2);const due=next.getFullYear()+'-'+String(next.getMonth()+1).padStart(2,'0')+'-'+String(next.getDate()).padStart(2,'0');
 return {...empty(),initialized:true,demo:true,courses:[{id:'c1',name:'Software Engineering',teacher:'Преподаватель',color:colors[0]},{id:'c2',name:'Базы данных',teacher:'Преподаватель',color:colors[1]},{id:'c3',name:'Английский язык',teacher:'Преподаватель',color:colors[2]}],
 lessons:[{id:'l1',courseId:'c1',day:weekday(),start:'09:00',end:'10:20',room:'C1.2.205',kind:'Лекция'},{id:'l2',courseId:'c2',day:weekday(),start:'10:30',end:'11:50',room:'C1.2.307',kind:'Лабораторная'},{id:'l3',courseId:'c3',day:weekday(),start:'13:00',end:'14:20',room:'C1.1.108',kind:'Практика'}],
 tasks:[{id:'t1',courseId:'c1',title:'Диаграмма классов',due:today(),priority:'Высокий',note:'Описать связи между сущностями проекта',done:false},{id:'t2',courseId:'c2',title:'Лабораторная: SQL JOIN',due,priority:'Обычный',note:'Подготовить пять запросов',done:false},{id:'t3',courseId:'c3',title:'Прочитать статью',due:today(),priority:'Обычный',note:'',done:true}],
 grades:[{id:'g1',courseId:'c1',title:'Практическая работа',score:92,max:100,weight:1},{id:'g2',courseId:'c2',title:'Лабораторная работа',score:18,max:20,weight:1}]};
}
