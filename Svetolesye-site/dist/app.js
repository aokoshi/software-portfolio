const regions=[
 {image:"world",name:"Тихая роща",title:"Первый шаг — в высокую траву.",description:"Выбери спутника, познакомься с жителями долины и собери три разных семейства для испытания Ветрокрона."},
 {image:"coast",name:"Зеркальный берег",title:"Три огня над зеркальной водой.",description:"Зажги маяки на западном, южном и восточном берегах. Когда загорятся все три, Приливень примет твоё испытание и откроет путь дальше."},
 {image:"summit",name:"Лунный перевал",title:"Выше троп. Ближе к звёздам.",description:"Победи стражей ветра и камня, подготовь команду и поднимись к Астрарону. Здесь сходятся истории трёх земель."}
];
const tabs=[...document.querySelectorAll("[data-region]")];
function selectRegion(index,focus=false){
 const region=regions[index];
 tabs.forEach((tab,i)=>{tab.classList.toggle("active",i===index);tab.setAttribute("aria-selected",String(i===index));tab.tabIndex=i===index?0:-1});
 document.querySelector("#region-panel").setAttribute("aria-labelledby","region-"+index);
 const image=document.querySelector("#region-image");image.src="assets/"+region.image+".png";image.alt="Карта: "+region.name;
 document.querySelector("#region-title").textContent=region.title;
 document.querySelector("#region-description").textContent=region.description;
 if(focus)tabs[index].focus();
}
tabs.forEach((tab,index)=>{tab.addEventListener("click",()=>selectRegion(index));tab.addEventListener("keydown",event=>{
 let next;if(event.key==="ArrowRight"||event.key==="ArrowDown")next=(index+1)%3;
 if(event.key==="ArrowLeft"||event.key==="ArrowUp")next=(index+2)%3;
 if(event.key==="Home")next=0;if(event.key==="End")next=2;
 if(next!==undefined){event.preventDefault();selectRegion(next,true)}
})});
const forms={moss:["Мшун","verdant","Мохорог"],spark:["Искрик","flare","Жаролис"],drop:["Каплик","tide","Акварин"]};
document.querySelectorAll("[data-evolution]").forEach(button=>button.addEventListener("click",()=>{
 const id=button.dataset.evolution;const [base,evolved,name]=forms[id];const active=button.getAttribute("aria-pressed")!=="true";const card=button.closest("article");
 button.setAttribute("aria-pressed",String(active));card.querySelector("img").src="assets/"+(active?evolved:id)+".png";card.querySelector("img").alt=active?name:base;
 card.querySelector("h3").textContent=active?name:base;card.querySelector(".form-label").textContent=active?"ЭВОЛЮЦИЯ · УР. 8":"ПЕРВАЯ ФОРМА";
 button.innerHTML=active?'Первая форма <span>↶</span>':'Посмотреть эволюцию <span>↗</span>';
}));
const audio=document.querySelector("#audio");const track=document.querySelector("#track");const player=document.querySelector(".music-player");const status=document.querySelector("#audio-status");
audio.volume=.45;
audio.addEventListener("play",()=>{player.classList.add("playing");status.textContent="Сейчас звучит: "+track.selectedOptions[0].textContent});
audio.addEventListener("pause",()=>{player.classList.remove("playing");status.textContent="На паузе. Продолжи, когда захочешь."});
audio.addEventListener("error",()=>{player.classList.remove("playing");status.textContent="Не удалось загрузить музыку. Попробуй другую мелодию."});
track.addEventListener("change",async()=>{const playing=!audio.paused;audio.src="assets/"+track.value+".wav";audio.load();status.textContent="Выбрано: "+track.selectedOptions[0].textContent;if(playing){try{await audio.play()}catch{status.textContent="Нажми кнопку воспроизведения, чтобы включить мелодию."}}});
document.querySelector("#back-top").addEventListener("click",event=>{event.preventDefault();window.scrollTo({top:0,behavior:matchMedia("(prefers-reduced-motion: reduce)").matches?"instant":"smooth"})});
