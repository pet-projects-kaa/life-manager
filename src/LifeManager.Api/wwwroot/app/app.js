const API = new URL('../api/', window.location.href);
const state = { me:null, view:'today', data:null, legalCategory:'auto' };
const titles = {today:'Сегодня',tasks:'Дела',habits:'Привычки',shopping:'Покупки',home:'Дом',benefits:'Не потеряй',legal:'Разобраться',advice:'Советы',profile:'Профиль'};
const content = document.querySelector('#content');
const modal = document.querySelector('#modal');
const modalBody = document.querySelector('#modal-body');
const authScreen = document.querySelector('#auth-screen');
const appShell = document.querySelector('#app-shell');
const moreSheet = document.querySelector('#more-sheet');

const esc = v => String(v ?? '').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
const fmtMoney = v => v == null ? '' : new Intl.NumberFormat('ru-RU',{maximumFractionDigits:0}).format(v)+' ₽';
const fmtDate = v => v ? new Intl.DateTimeFormat('ru-RU',{day:'numeric',month:'short'}).format(new Date(v)) : '';
const fmtDateTime = v => v ? new Intl.DateTimeFormat('ru-RU',{day:'numeric',month:'short',hour:'2-digit',minute:'2-digit'}).format(new Date(v)) : '';
const daysUntil = v => v ? Math.ceil((new Date(v)-new Date())/86400000) : null;
const initials = name => (name?.trim()?.[0] || 'L').toUpperCase();
const iconFor = kind => ({weather:'☔',plan:'✓',habit:'◎',home:'⌂',benefit:'₽',subscription:'↻',warranty:'🛡',insurance:'🚗',deadline:'!',horoscope:'✦',mood:'☺',hobby:'◌',reading:'📖'}[kind] || '✦');

async function api(path, options={}){
  const url = new URL(path.replace(/^\//,''), API);
  const headers = new Headers(options.headers || {});
  if(options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) headers.set('Content-Type','application/json');
  const res = await fetch(url,{credentials:'include',...options,headers});
  if(res.status===401){ showAuth(); throw new Error('unauthorized'); }
  if(!res.ok){ let msg='Ошибка запроса'; try{ const x=await res.json(); msg=x.error||msg; }catch{} throw new Error(msg); }
  if(res.status===204) return null;
  const type=res.headers.get('content-type')||'';
  return type.includes('json') ? res.json() : res.text();
}

function toast(text){ const el=document.querySelector('#toast'); el.textContent=text; el.classList.remove('hidden'); clearTimeout(toast.t); toast.t=setTimeout(()=>el.classList.add('hidden'),2400); }
function showAuth(){ authScreen.classList.remove('hidden'); appShell.classList.add('hidden'); }
function showApp(){ authScreen.classList.add('hidden'); appShell.classList.remove('hidden'); }
function loader(text='Загружаем…'){ content.innerHTML=`<div class="loader"><span></span><p>${esc(text)}</p></div>`; }
function empty(title,text,action=''){ return `<div class="empty"><b>${esc(title)}</b><span>${esc(text)}</span>${action}</div>`; }
function openModal(html){ modalBody.innerHTML=html; modal.classList.remove('hidden'); }
function closeModal(){ modal.classList.add('hidden'); modalBody.innerHTML=''; }
function pageHead(kicker,title,subtitle,action=''){return `<div class="page-head"><div><div class="eyebrow">${esc(kicker)}</div><h1>${esc(title)}</h1><p>${esc(subtitle)}</p></div>${action||`<span class="date-chip">${new Intl.DateTimeFormat('ru-RU',{day:'numeric',month:'long',weekday:'short'}).format(new Date())}</span>`}</div>`}
function setProfileUI(){ const name=state.me?.displayName||'Профиль'; document.querySelector('#side-name').textContent=name; document.querySelector('#top-name').textContent=name; document.querySelector('#side-avatar').textContent=initials(name); document.querySelector('#top-avatar').textContent=initials(name); }
function setActiveView(view){ document.querySelectorAll('[data-view]').forEach(x=>x.classList.toggle('active',x.dataset.view===view)); document.querySelectorAll('.mobile-nav [data-view]').forEach(x=>x.classList.toggle('active',x.dataset.view===view)); document.querySelector('#page-title').textContent=titles[view]||'Life'; }

async function route(view, push=true){
  state.view=view; setActiveView(view); if(push) history.replaceState({},'',`#${view}`); moreSheet.classList.add('hidden'); loader();
  try{
    if(view==='today') await renderToday();
    else if(view==='tasks') await renderTasks();
    else if(view==='habits') await renderHabits();
    else if(view==='shopping') await renderShopping();
    else if(view==='home') await renderHome();
    else if(view==='benefits') await renderBenefits();
    else if(view==='legal') await renderLegal();
    else if(view==='advice') await renderAdvice();
    else if(view==='profile') await renderProfile();
  }catch(e){ if(e.message!=='unauthorized') content.innerHTML=empty('Не получилось загрузить экран',e.message,`<button class="secondary" data-retry>Повторить</button>`); }
}

async function bootstrap(){
  try{ state.me=await api('auth/me'); showApp(); setProfileUI(); const view=location.hash.slice(1); await route(titles[view]?view:'today',false); }
  catch{ showAuth(); }
  if('serviceWorker' in navigator) navigator.serviceWorker.register('sw.js').catch(()=>{});
}

async function renderToday(){
  const d=await api('dashboard'); state.data=d; const weather=d.weather; const tasks=d.tasks||[]; const habits=d.habits||[]; const advice=d.advice||[]; const nl=d.notLose||[]; const facts=d.todayFacts||[];
  const firstName=(d.profile.displayName||'').split(' ')[0]||'Друг';
  const weatherMeta=weather.available
    ? `${weather.minTemperature!=null&&weather.maxTemperature!=null?`${weather.minTemperature}…${weather.maxTemperature}° · `:''}${weather.precipitationProbability!=null?`осадки до ${weather.precipitationProbability}% · `:''}${esc(weather.source||'Open-Meteo')}`
    : esc(weather.source||'Open-Meteo');
  const mood=d.todayMood;
  const dayKey=new Date().toISOString().slice(0,10);
  let cachedImage=''; try{cachedImage=sessionStorage.getItem(`life-day-image:${dayKey}`)||'';}catch{}
  content.innerHTML=pageHead('Сегодня',`Доброе утро, ${firstName} ☀️`,'Собрали главное на сегодня — без лишнего шума.')+`
  <div class="grid two">
    <section class="card weather-card"><div><div class="eyebrow">Погода · ${esc(weather.city)}</div><b class="big">${weather.available?esc(weather.summary):'Нет данных'}</b><p>${esc(weather.outfitAdvice)}</p><small class="tiny">${weatherMeta}</small></div><div class="weather-art">${weather.precipitationProbability>=50?'☔':'☀️'}</div></section>
    <section class="card horoscope-card"><div class="label">Для настроения · ${esc(d.horoscope.sign)} · ${esc(d.horoscope.themeTitle||'день')}</div><h3>Гороскоп на сегодня</h3><p>${esc(d.horoscope.text)}</p><small class="tiny">${esc(d.horoscope.disclaimer)}</small></section>
  </div>
  <section class="card mood-card" style="margin-top:16px"><div class="card-title"><div><h2>Как ты сегодня?</h2><p class="tiny">Отметка влияет на советы дня, но никуда больше не интерпретируется.</p></div>${mood?`<span class="advice-label">энергия ${mood.energy}/5</span>`:''}</div><div class="mood-picker">${[['great','😄','Отлично',5],['good','🙂','Нормально',4],['neutral','😐','Ровно',3],['tired','😴','Устала',2],['low','🌧️','Не очень',1]].map(([k,i,n,e])=>`<button class="mood-option ${mood?.mood===k?'active':''}" data-mood="${k}" data-energy="${e}"><span>${i}</span><small>${n}</small></button>`).join('')}</div></section>
  <div class="grid two" style="margin-top:16px">
    <section class="card"><div class="card-title"><h2>Сегодня</h2><button class="ghost" data-view="tasks">Все дела</button></div><div class="list">${tasks.length?tasks.slice(0,5).map(taskRow).join(''):empty('День свободен','Добавь дело, если оно действительно нужно.')}</div></section>
    <section class="card"><div class="card-title"><h2>Привычки</h2><button class="ghost" data-view="habits">Открыть</button></div>${habits.length?habits.slice(0,4).map(h=>`<div class="habit-row"><div class="icon-box green">${esc(h.icon)}</div><div><b>${esc(h.title)}</b><small class="tiny">${h.value}/${h.target} ${esc(h.unit)}</small></div><div class="progress"><i style="width:${Math.round((h.progress||0)*100)}%"></i></div><div class="habit-actions"><button data-habit-change="${h.id}" data-delta="1">+</button></div></div>`).join(''):empty('Привычек пока нет','Можно начать с одной простой привычки.')}</section>
  </div>
  <div class="grid two" style="margin-top:16px">
    <section class="card"><div class="card-title"><h2>Совет дня</h2><button class="ghost" data-view="advice">Все советы</button></div>${advice[0]?adviceCard(advice[0],false):empty('Пока всё спокойно','Новых советов на сегодня нет.')}</section>
    <section class="card smart-card green"><div class="card-title"><h2>Не потеряй</h2><button class="ghost" data-view="benefits">Открыть</button></div>${nl[0]?`<div class="advice"><div class="icon-box green">${iconFor(nl[0].kind)}</div><div><h3>${esc(nl[0].title)}</h3><p>${esc(nl[0].text)}</p><span class="advice-label">${nl[0].amount?fmtMoney(nl[0].amount):nl[0].dueAt?'до '+fmtDate(nl[0].dueAt):'Проверить'}</span></div></div>`:empty('Ничего срочного','И это хороший знак.')}</section>
  </div>
  <section class="card day-image-card" style="margin-top:16px"><div class="card-title"><div><h2>Картинка дня</h2><p class="tiny">Генерируется по теме гороскопа и сегодняшнему настроению. Без нашего API-ключа: через Puter.js.</p></div><button class="secondary" data-generate-day-image>${cachedImage?'Сгенерировать другую':'Сгенерировать'}</button></div><div id="day-image-slot" class="day-image-slot">${cachedImage?`<img src="${cachedImage}" alt="Картинка дня">`:`<div class="image-placeholder"><span>✦</span><b>Сегодняшний визуальный образ появится здесь</b><small>При первом запуске Puter может попросить одноразовое согласие на временную анонимную сессию.</small></div>`}</div></section>
  <section class="card" style="margin-top:16px"><div class="card-title"><h2>Сегодня в истории</h2><span class="tiny">Википедия · факты на ${new Intl.DateTimeFormat('ru-RU',{day:'numeric',month:'long'}).format(new Date())}</span></div>${facts.length?`<div class="fact-list">${facts.map(f=>`<div class="step"><span class="num">${f.year??'•'}</span><div><div>${esc(f.text)}</div><a class="advice-label" href="${esc(f.sourceUrl)}" target="_blank" rel="noopener">${esc(f.sourceTitle||'Источник')} ↗</a></div></div>`).join('')}</div>`:empty('Факты не загрузились','Попробуем снова при следующем обновлении.')}</section>`;
}

function taskRow(t){ return `<div class="row"><button class="check ${t.isCompleted?'done':''}" data-task-toggle="${t.id}">${t.isCompleted?'✓':''}</button><div class="row-main"><b>${esc(t.title)}</b><small>${t.repeatEveryDays?`Повтор · каждые ${t.repeatEveryDays} дн.`:t.notes?esc(t.notes):'Разовое дело'}</small></div><span class="row-meta">${t.dueAt?fmtDateTime(t.dueAt):t.priority==='high'?'Важно':''}</span></div>`; }
function adviceCard(a,feedback=true){ return `<div class="advice"><div class="icon-box ${a.kind==='benefit'?'green':a.kind==='horoscope'?'purple':''}">${iconFor(a.kind)}</div><div><h3>${esc(a.title)}</h3><p>${esc(a.text)}</p><span class="advice-label">${esc(a.label)}</span></div>${feedback?`<div class="feedback"><button title="Полезно" data-advice-feedback="${esc(a.key)}" data-advice-kind="${esc(a.kind)}" data-useful="true">👍</button><button title="Не полезно" data-advice-feedback="${esc(a.key)}" data-advice-kind="${esc(a.kind)}" data-useful="false">👎</button></div>`:''}</div>`; }

async function renderTasks(){
  const items=await api('tasks'); const today=items.filter(x=>!x.isCompleted&&!x.repeatEveryDays); const recurring=items.filter(x=>x.repeatEveryDays&&!x.isCompleted); const done=items.filter(x=>x.isCompleted);
  content.innerHTML=pageHead('План','Дела','Разовые и регулярные задачи в одном спокойном списке.',`<button class="primary" data-open-form="task">+ Добавить дело</button>`)+`
  <section class="card smart-card green"><div class="label">Оценка дня</div><h3>${today.length<=5?'План выглядит реалистично':'Сегодня дел многовато'}</h3><p>${today.length?`Сейчас ${today.length} активных разовых дел. ${today.length>5?'Выбери несколько обязательных, остальное лучше перенести.':'Такой объём проще действительно закрыть.'}`:'На сегодня нет разовых задач.'}</p></section>
  <section class="card" style="margin-top:16px"><div class="card-title"><h2>Сегодня</h2><span class="tiny">${today.length} активных</span></div><div class="list">${today.length?today.map(taskRow).join(''):empty('Свободный день','Можно ничего не добавлять — это тоже нормальный план.')}</div></section>
  <section class="card" style="margin-top:16px"><div class="card-title"><h2>Регулярные дела</h2><span class="tiny">следующая дата пересчитывается автоматически</span></div><div class="list">${recurring.length?recurring.map(taskRow).join(''):empty('Регулярных дел нет','Добавь повторение при создании задачи.')}</div></section>
  ${done.length?`<section class="card" style="margin-top:16px"><div class="card-title"><h2>Выполнено</h2></div><div class="list">${done.slice(0,8).map(taskRow).join('')}</div></section>`:''}`;
}

async function renderHabits(){
  const items=await api('habits');
  content.innerHTML=pageHead('Привычки','Маленькие действия','Без наказаний за пропуски: важна динамика, а не идеальная серия.',`<button class="primary" data-open-form="habit">+ Добавить</button>`)+`
  <section class="card"><div class="list">${items.length?items.map(h=>`<div class="habit-row"><div class="icon-box green">${esc(h.icon)}</div><div><b>${esc(h.title)}</b><small class="tiny">Сегодня ${h.value}/${h.target} ${esc(h.unit)} · за 30 дней ${h.stats.completedDays}/${h.stats.totalDays}</small></div><div class="habit-progress"><strong>${Math.min(100,Math.round(h.value/h.target*100))}%</strong><div class="progress"><i style="width:${Math.min(100,Math.round(h.value/h.target*100))}%"></i></div></div><div class="habit-actions"><button data-habit-change="${h.id}" data-delta="-1">−</button><button data-habit-change="${h.id}" data-delta="1">+</button></div></div>`).join(''):empty('Нет привычек','Добавь одну привычку, которую реально хочется поддерживать.')}</div></section>
  ${items.length?`<section class="card smart-card" style="margin-top:16px"><div class="label">Умное предложение</div><h3>Цели можно менять под реальную жизнь</h3><p>${items.some(x=>x.stats.betterOnWeekdays)?'По некоторым привычкам будни сейчас стабильнее выходных.':'По части привычек выходные идут не хуже будней.'} Мы не обнуляем прогресс из-за одного пропуска.</p></section>`:''}`;
}

async function renderShopping(){
  const items=await api('shopping'); const total=items.filter(x=>!x.isPurchased).reduce((s,x)=>s+(x.estimatedPrice||0),0); const open=items.filter(x=>!x.isPurchased); const done=items.filter(x=>x.isPurchased);
  content.innerHTML=pageHead('План','Покупки','Можно добавлять по одному или надиктовать целый список голосом.',`<div class="head-actions"><button class="secondary" data-voice-shopping>🎙 Надиктовать</button><button class="primary" data-open-form="shopping">+ Добавить</button></div>`)+`
  <section class="card voice-shopping-hint"><div class="icon-box purple">🎙</div><div><h3>Скажи список как обычно</h3><p class="muted">Например: «молоко, яйца, корм коту, таблетки для посудомойки». Перед сохранением ты увидишь распознанный текст и сможешь поправить его.</p></div></section>
  <div class="chips" style="margin:16px 0"><span class="chip active">Все</span><span class="chip">Продукты</span><span class="chip">Дом</span><span class="chip">Питомец</span><span class="chip">Прочее</span></div>
  <section class="card"><div class="list">${open.length?open.map(shopRow).join(''):empty('Список пуст','Нечего покупать — отлично.')}</div><div class="shopping-summary" style="margin-top:14px"><span>Примерная сумма</span><b>${fmtMoney(total)}</b></div></section>
  <div class="grid two" style="margin-top:16px"><section class="card smart-card"><div class="label">Связано с домом</div><h3>Покупка может стать регулярным делом</h3><p>Например, фильтр после покупки можно добавить в «Дом» с циклом замены.</p></section><section class="card"><div class="card-title"><h3>Чек или крупная покупка</h3></div><p class="muted">Добавь отдельную покупку с суммой и гарантией — она попадёт в «Не потеряй».</p><button class="secondary" data-open-form="purchase">Добавить покупку</button></section></div>
  ${done.length?`<section class="card" style="margin-top:16px"><div class="card-title"><h2>Куплено</h2></div><div class="list">${done.slice(0,10).map(shopRow).join('')}</div></section>`:''}`;
}
function shopRow(x){return `<div class="row"><button class="check ${x.isPurchased?'done':''}" data-shopping-toggle="${x.id}">${x.isPurchased?'✓':''}</button><div class="row-main"><b>${esc(x.title)}</b><small>${esc(({food:'Продукты',home:'Дом',pet:'Питомец',medical:'Здоровье',education:'Образование',fitness:'Фитнес',appliance:'Техника',other:'Прочее'})[x.category]||x.category)}</small></div><span class="row-meta">${x.estimatedPrice?fmtMoney(x.estimatedPrice):''}</span></div>`}

async function renderHome(){
  const items=await api('home'); const groups={chore:'Сегодня дома',consumable:'Расходники',appliance:'Техника и гарантия',pet:'Питомец'};
  content.innerHTML=pageHead('Дом','Дом','Бытовые циклы, расходники, техника и питомцы — то, что обычно приходится держать в голове.',`<button class="primary" data-open-form="home">+ Добавить</button>`)+Object.entries(groups).map(([key,title])=>{
    const xs=items.filter(x=>x.category===key); return `<section class="card home-group"><div class="card-title"><h2>${title}</h2><span class="tiny">${xs.length}</span></div><div class="list">${xs.length?xs.map(homeRow).join(''):empty('Пока пусто','Здесь появятся соответствующие домашние дела.')}</div></section>`
  }).join('');
}
function homeRow(x){ const due=x.nextDueAt?daysUntil(x.nextDueAt):null; const meta=x.daysRemaining!=null?`осталось на ${x.daysRemaining} дн.`:due!=null?(due<=0?'пора сделать':`через ${due} дн.`):''; return `<div class="row"><div class="icon-box ${x.category==='pet'?'purple':x.category==='consumable'?'green':''}">${x.category==='pet'?'🐾':x.category==='appliance'?'🛡':x.category==='consumable'?'▣':'✓'}</div><div class="row-main"><b>${esc(x.title)}</b><small>${esc(x.subtitle||'')}</small></div><span class="row-meta ${due!=null&&due<=1?'deadline':''}">${esc(meta)}</span>${x.category==='chore'||x.repeatEveryDays?`<button class="ghost" data-home-complete="${x.id}">Готово</button>`:''}</div>` }

async function renderBenefits(){
  const [items,watch]=await Promise.all([api('benefits'),api('watch')]); const potential=items.filter(x=>x.amount).reduce((s,x)=>s+(x.amount||0),0);
  content.innerHTML=pageHead('Выгода','Не потеряй','Деньги, гарантии, подписки и сроки, которые лучше увидеть заранее.',`<button class="primary" data-open-form="watch">+ Добавить срок</button>`)+`
  <section class="card hero-card"><div class="eyebrow" style="color:#d9fff6">Под контролем</div><div class="amount">${items.length} сигналов</div><p>${potential?`Суммы под наблюдением: ${fmtMoney(potential)}`:'Система следит за сроками и гарантиями.'}</p></section>
  <section class="card" style="margin-top:16px"><div class="card-title"><h2>Что требует внимания</h2></div><div class="list">${items.length?items.map(benefitRow).join(''):empty('Ничего срочного','Добавь гарантию, подписку или важный срок.')}</div></section>
  <section class="card smart-card orange" style="margin-top:16px"><div class="label">Цена бездействия</div><h3>Сроки здесь важнее красивых цифр</h3><p>Если у пункта есть сумма, она показывается как ориентир из введённых тобой данных. Для льгот и вычетов приложение предлагает проверить условия, а не обещает выплату.</p></section>
  <section class="card" style="margin-top:16px"><div class="card-title"><h2>Ручной контроль</h2></div><div class="list">${watch.length?watch.map(x=>`<div class="row"><div class="icon-box">${iconFor(x.kind)}</div><div class="row-main"><b>${esc(x.title)}</b><small>${esc(x.note||x.kind)}</small></div><span class="row-meta">${x.dueAt?fmtDate(x.dueAt):''}</span>${!x.isResolved?`<button class="ghost" data-watch-resolve="${x.id}">Готово</button>`:'<span class="benefit-badge">закрыто</span>'}</div>`).join(''):''}</div></section>`;
}
function benefitRow(x){return `<div class="row benefit-row"><div class="icon-box ${x.kind==='benefit'?'green':''}">${iconFor(x.kind)}</div><div class="row-main"><b>${esc(x.title)}</b><small>${esc(x.text)}</small>${x.sourceUrl?`<a class="advice-label" href="${esc(x.sourceUrl)}" target="_blank" rel="noopener">Официальный источник ↗</a>`:''}</div><span class="row-meta">${x.amount?fmtMoney(x.amount):x.dueAt?fmtDate(x.dueAt):''}</span></div>`}

async function renderLegal(){
  content.innerHTML=pageHead('Помощник','Разобраться','Опиши ситуацию свободным текстом. Категория — только подсказка: решение выбирается прежде всего по самому описанию.')+`
  <section class="card legal-box"><div class="legal-categories">${[['auto','✨ По тексту'],['consumer','🛒 Покупки/услуги'],['housing','🏠 Жильё'],['work','💼 Работа'],['bank','💳 Банк'],['tax','₽ Налоги'],['privacy','🔐 Данные'],['auto','🚗 Авто'],['family','👨‍👩‍👧 Семья']].map(([k,n])=>`<button class="${state.legalCategory===k?'active':''}" data-legal-cat="${k}">${n}</button>`).join('')}</div><textarea id="legal-text" rows="8" placeholder="Например: 12 августа оплатила заказ в интернет-магазине. Доставить обещали до 20 августа, но товар до сих пор не приехал. Продавец пишет, что срок неизвестен. Хочу отказаться от заказа и вернуть деньги."></textarea><div class="tiny" style="margin-top:8px">Лучше указать: кто вторая сторона, даты, сумму, документы и чего именно ты хочешь добиться.</div><button class="primary" style="margin-top:12px" data-legal-submit>Разобраться</button></section><div id="legal-result" style="margin-top:16px"></div>`;
}
function renderLegalResult(a){
  const confidence=a.confidence||0; const questions=a.followUpQuestions||[]; const signals=a.matchedSignals||[];
  document.querySelector('#legal-result').innerHTML=`<section class="card"><div class="card-title"><div><h2>${esc(a.title)}</h2><div class="legal-meta"><span class="advice-label">${esc(a.category)}</span>${confidence?`<span class="confidence">уверенность ${confidence}%</span>`:''}</div></div></div>${confidence?`<div class="confidence-bar"><i style="width:${confidence}%"></i></div>`:''}<p class="muted">${esc(a.summary)}</p>${signals.length?`<div class="signal-list"><small>Что распознано:</small>${signals.map(x=>`<span>${esc(x)}</span>`).join('')}</div>`:''}<div>${a.steps.map((st,i)=>`<div class="step"><span class="num">${i+1}</span><div>${esc(st)}</div></div>`).join('')}</div>${questions.length?`<div class="follow-ups"><h3>Что уточнить для более точного ответа</h3>${questions.map(q=>`<div>• ${esc(q)}</div>`).join('')}</div>`:''}${a.sources?.length?`<div class="sources"><h3>Источники для проверки</h3>${a.sources.map(src=>`<a href="${esc(src.url)}" target="_blank" rel="noopener"><b>${esc(src.title)} ↗</b>${src.note?`<small>${esc(src.note)}</small>`:''}</a>`).join('')}</div>`:''}<div class="disclaimer">${esc(a.disclaimer)}</div></section>`;
}

async function renderAdvice(){
  const [items,reading]=await Promise.all([api('advice'),api('reading-suggestions')]);
  content.innerHTML=pageHead('Для тебя','Советы','Здесь учитываются дела, погода, настроение, интересы и развлекательная тема гороскопа.')+`
  <section class="card reading-card"><div class="card-title"><div><h2>Что интересно почитать</h2><p class="tiny">Подборка меняется ежедневно по интересам из профиля и теме дня.</p></div><span class="advice-label">Википедия</span></div>${reading.length?`<div class="reading-grid">${reading.map(r=>`<a href="${esc(r.url)}" target="_blank" rel="noopener" class="reading-item"><span>📖</span><div><b>${esc(r.title)}</b><p>${esc(r.snippet)}</p><small>${esc(r.topic)} · открыть ↗</small></div></a>`).join('')}</div>`:empty('Пока ничего не подобрали','Добавь интересы и хобби в профиле или обнови страницу чуть позже.')}</section>
  <div class="advice-list" style="margin-top:16px">${items.length?items.map(a=>`<section class="card">${adviceCard(a,true)}</section>`).join(''):empty('Советов пока нет','Здесь появятся только полезные сигналы.')}</div>`;
}

async function renderProfile(){
  const p=await api('profile');
  content.innerHTML=pageHead('Аккаунт','Профиль','Город нужен для погоды, а интересы и хобби — для персональных советов и чтения.',`<button class="ghost" data-logout>Выйти</button>`)+`
  <div class="profile-grid"><section class="card"><div class="profile-person"><span class="big-avatar">${initials(p.displayName)}</span><div><h2>${esc(p.displayName)}</h2><p class="muted">${esc(state.me.email||'')}</p></div></div><form id="profile-form" class="form-grid"><label>Имя<input name="displayName" value="${esc(p.displayName)}" required></label><label>Город<input name="city" value="${esc(p.city)}" required placeholder="Например, Челябинск"></label><label>Знак зодиака<select name="zodiacSign">${['Овен','Телец','Близнецы','Рак','Лев','Дева','Весы','Скорпион','Стрелец','Козерог','Водолей','Рыбы'].map(x=>`<option ${x===p.zodiacSign?'selected':''}>${x}</option>`).join('')}</select></label><label>Стиль одежды<select name="clothingStyle"><option value="casual" ${p.clothingStyle==='casual'?'selected':''}>Casual</option><option value="classic" ${p.clothingStyle==='classic'?'selected':''}>Классический</option><option value="sport" ${p.clothingStyle==='sport'?'selected':''}>Спортивный</option></select></label><label class="full">Интересы и хобби<textarea name="interests" rows="3" placeholder="Например: интерьер, история, книги, психология, C#, фотография">${esc(p.interests||'')}</textarea><small class="tiny">Через запятую. Используем для советов и блока «Что почитать».</small></label><div class="full"><button class="primary" type="submit">Сохранить</button></div></form></section><section class="card"><div class="card-title"><h2>На телефоне</h2></div><h3>PWA</h3><p class="muted">Открой меню браузера → «Добавить на экран Домой». Life Manager будет запускаться почти как обычное приложение.</p><hr style="border:0;border-top:1px solid var(--line);margin:22px 0"><h3>Откуда берутся данные</h3><p class="muted">Погода — Open-Meteo по городу. Факты и чтение — Википедия. Юридический помощник подбирает сценарий по тексту и прикладывает КонсультантПлюс и профильные официальные ресурсы. Картинка дня генерируется на клиенте через Puter.js — наш сервер не хранит ключ AI-провайдера.</p></section></div>`;
}

function parseShoppingTranscript(text){
  let clean=(text||'').replace(/\b(?:добавь|добавить|купи|купить|нужно купить|надо купить|запиши|в список)\b/gi,' ').replace(/\s+/g,' ').trim();
  return clean.split(/[,;\n.!?]+|\s+(?:и еще|ещ[её]|плюс)\s+/i).map(x=>x.trim().replace(/^и\s+/i,'')).filter(x=>x.length>0&&x.length<=160).slice(0,50);
}
function voiceShoppingModal(){
  openModal(`<h2>🎙 Голосовой список</h2><p class="muted">Говори естественно: «молоко, яйца, корм коту, таблетки для посудомойки». Можно исправить текст перед добавлением.</p><div class="voice-status" id="voice-status">Нажимаем микрофон…</div><textarea id="voice-shopping-text" rows="6" placeholder="Распознанный список появится здесь"></textarea><div class="form-actions"><button type="button" class="ghost" data-voice-stop>Остановить</button><button type="button" class="primary" data-voice-add>Добавить список</button></div>`);
}
function startVoiceShopping(){
  const Recognition=window.SpeechRecognition||window.webkitSpeechRecognition;
  voiceShoppingModal();
  if(!Recognition){ document.querySelector('#voice-status').textContent='В этом браузере нет SpeechRecognition. Можно продиктовать в системную клавиатуру или вписать список сюда вручную.'; return; }
  const rec=new Recognition(); window.__lifeShoppingRecognition=rec; rec.lang='ru-RU'; rec.continuous=true; rec.interimResults=true;
  let finalText=''; const area=document.querySelector('#voice-shopping-text'), status=document.querySelector('#voice-status');
  rec.onstart=()=>{status.textContent='Слушаю… говори список. Браузер может запросить доступ к микрофону.';status.classList.add('listening')};
  rec.onresult=e=>{let interim='';for(let i=e.resultIndex;i<e.results.length;i++){const t=e.results[i][0].transcript;if(e.results[i].isFinal)finalText+=t+', ';else interim+=t;}area.value=(finalText+interim).trim();};
  rec.onerror=e=>{status.textContent=e.error==='not-allowed'?'Нет доступа к микрофону. Разреши его в настройках браузера.':`Не получилось распознать: ${e.error}`;status.classList.remove('listening')};
  rec.onend=()=>{status.textContent=area.value?'Готово. Проверь текст и добавь список.':'Запись остановлена.';status.classList.remove('listening')};
  try{rec.start()}catch{status.textContent='Микрофон уже запущен.'}
}
async function addVoiceShopping(){
  const text=document.querySelector('#voice-shopping-text')?.value||''; const items=parseShoppingTranscript(text);
  if(!items.length){toast('Не вижу товаров в тексте');return;}
  try{window.__lifeShoppingRecognition?.stop()}catch{}
  const created=await api('shopping/bulk',{method:'POST',body:JSON.stringify({items})}); closeModal(); toast(`Добавлено: ${created.length}`); await route('shopping',false);
}
function dayImagePrompt(){
  const d=state.data||{}; const mood=d.todayMood?.mood||'neutral'; const moodText={great:'bright and energetic',good:'calm and pleasant',neutral:'balanced and quiet',tired:'soft and restful',low:'gentle and comforting'}[mood]||'balanced';
  const theme=d.horoscope?.themeTitle||'focus'; const city=d.profile?.city||'';
  return `A beautiful editorial illustration for a personal daily journal. Mood: ${moodText}. Theme of the day: ${theme}. Subtle atmosphere inspired by ${city}. Modern minimal composition, sophisticated natural light, calm premium lifestyle aesthetic, no people close-up, no logos, no text, no letters, no typography, landscape 16:9.`;
}
async function generateDayImage(){
  const slot=document.querySelector('#day-image-slot'), btn=document.querySelector('[data-generate-day-image]'); if(!slot||!btn)return;
  if(!window.puter?.ai?.txt2img){toast('Генератор изображений пока не загрузился');return;}
  btn.disabled=true; btn.textContent='Генерируем…'; slot.innerHTML='<div class="image-placeholder generating"><span>✦</span><b>Собираем картинку дня…</b><small>Это может занять немного времени.</small></div>';
  try{
    if(window.puter.auth && !window.puter.auth.isSignedIn()) await window.puter.auth.signIn({attempt_temp_user_creation:true});
    const img=await window.puter.ai.txt2img(dayImagePrompt(),{provider:'replicate-image-generation',model:'black-forest-labs/flux-schnell',ratio:{w:16,h:9},steps:4});
    img.alt='Картинка дня'; slot.innerHTML=''; slot.appendChild(img); try{sessionStorage.setItem(`life-day-image:${new Date().toISOString().slice(0,10)}`,img.src)}catch{}
    btn.textContent='Сгенерировать другую';
  }catch(e){slot.innerHTML=`<div class="image-placeholder"><span>×</span><b>Не получилось сгенерировать</b><small>${esc(e?.message||'Публичный AI-сервис временно недоступен')}</small></div>`;btn.textContent='Попробовать ещё';}
  finally{btn.disabled=false;}
}

function openForm(type){
  const configs={
    task:{title:'Новое дело',fields:`<label>Название<input name="title" required placeholder="Например, забрать заказ"></label><label>Когда<input name="dueAt" type="datetime-local"></label><label>Приоритет<select name="priority"><option value="normal">Обычный</option><option value="high">Важный</option></select></label><label>Повторять каждые N дней<input name="repeatEveryDays" type="number" min="1" placeholder="Например, 7"></label><label class="full">Комментарий<textarea name="notes" rows="3"></textarea></label>`},
    habit:{title:'Новая привычка',fields:`<label>Название<input name="title" required placeholder="Вода"></label><label>Иконка<input name="icon" value="✓" maxlength="4"></label><label>Цель<input name="target" type="number" min="1" value="1" required></label><label>Единица<input name="unit" value="раз" required></label>`},
    shopping:{title:'Добавить в покупки',fields:`<label>Название<input name="title" required></label><label>Категория<select name="category"><option value="food">Продукты</option><option value="home">Дом</option><option value="pet">Питомец</option><option value="medical">Здоровье</option><option value="education">Образование</option><option value="fitness">Фитнес</option><option value="appliance">Техника</option><option value="other">Прочее</option></select></label><label>Примерная цена<input name="estimatedPrice" type="number" min="0" step="0.01"></label>`},
    purchase:{title:'Крупная покупка',fields:`<label>Что купили<input name="title" required></label><label>Категория<select name="category"><option value="appliance">Техника</option><option value="medical">Лечение</option><option value="education">Образование</option><option value="fitness">Фитнес</option><option value="other">Прочее</option></select></label><label>Сумма<input name="amount" type="number" min="0.01" step="0.01" required></label><label>Дата покупки<input name="purchasedAt" type="date"></label><label>Гарантия до<input name="warrantyUntil" type="date"></label>`},
    home:{title:'Добавить в дом',fields:`<label>Название<input name="title" required></label><label>Категория<select name="category"><option value="chore">Домовое дело</option><option value="consumable">Расходник</option><option value="appliance">Техника</option><option value="pet">Питомец</option></select></label><label>Подпись<input name="subtitle" placeholder="Кухня / фильтр / препарат"></label><label>Повтор каждые N дней<input name="repeatEveryDays" type="number" min="1"></label><label>Следующая дата<input name="nextDueAt" type="date"></label><label>Осталось дней<input name="daysRemaining" type="number" min="0"></label>`},
    watch:{title:'Не потерять',fields:`<label>Название<input name="title" required placeholder="ОСАГО / подписка / документ"></label><label>Тип<select name="kind"><option value="deadline">Срок</option><option value="subscription">Подписка</option><option value="insurance">Страховка</option><option value="warranty">Гарантия</option><option value="other">Другое</option></select></label><label>Срок<input name="dueAt" type="date"></label><label>Сумма / цена бездействия<input name="amount" type="number" min="0" step="0.01"></label><label class="full">Комментарий<textarea name="note" rows="3"></textarea></label>`}
  };
  const c=configs[type]; if(!c)return; openModal(`<h2>${c.title}</h2><form id="entity-form" data-entity-type="${type}" class="form-grid">${c.fields}<div class="form-actions full"><button type="button" class="ghost" data-close-modal>Отмена</button><button class="primary" type="submit">Сохранить</button></div></form>`);
}

async function submitEntity(form){
  const fd=new FormData(form), type=form.dataset.entityType, obj=Object.fromEntries(fd.entries());
  for(const k of ['estimatedPrice','amount','target','repeatEveryDays','daysRemaining']) if(obj[k]==='') obj[k]=null; else if(obj[k]!=null) obj[k]=Number(obj[k]);
  for(const k of ['dueAt','purchasedAt','warrantyUntil','nextDueAt']) if(obj[k]) obj[k]=new Date(obj[k]).toISOString(); else obj[k]=null;
  if(type==='task') await api('tasks',{method:'POST',body:JSON.stringify(obj)});
  if(type==='habit') await api('habits',{method:'POST',body:JSON.stringify(obj)});
  if(type==='shopping') await api('shopping',{method:'POST',body:JSON.stringify(obj)});
  if(type==='purchase') await api('purchases',{method:'POST',body:JSON.stringify(obj)});
  if(type==='home') await api('home',{method:'POST',body:JSON.stringify(obj)});
  if(type==='watch') await api('watch',{method:'POST',body:JSON.stringify({...obj,sourceUrl:null})});
  closeModal(); toast('Сохранено'); await route(state.view,false);
}

document.addEventListener('click',async e=>{
  const view=e.target.closest('[data-view]'); if(view){ await route(view.dataset.view); return; }
  if(e.target.closest('[data-mobile-more]')){ moreSheet.classList.remove('hidden'); return; }
  if(e.target.closest('[data-close-sheet]')){ moreSheet.classList.add('hidden'); return; }
  if(e.target.closest('[data-close-modal]')){ closeModal(); return; }
  const formBtn=e.target.closest('[data-open-form]'); if(formBtn){ openForm(formBtn.dataset.openForm); return; }
  const tt=e.target.closest('[data-task-toggle]'); if(tt){ await api(`tasks/${tt.dataset.taskToggle}/toggle`,{method:'POST'}); await route(state.view,false); return; }
  const hc=e.target.closest('[data-habit-change]'); if(hc){ await api(`habits/${hc.dataset.habitChange}/change?delta=${hc.dataset.delta}`,{method:'POST'}); await route(state.view,false); return; }
  const st=e.target.closest('[data-shopping-toggle]'); if(st){ await api(`shopping/${st.dataset.shoppingToggle}/toggle`,{method:'POST'}); await route(state.view,false); return; }
  const home=e.target.closest('[data-home-complete]'); if(home){ await api(`home/${home.dataset.homeComplete}/complete`,{method:'POST'}); toast('Домовое дело обновлено'); await route('home',false); return; }
  const wr=e.target.closest('[data-watch-resolve]'); if(wr){ await api(`watch/${wr.dataset.watchResolve}/resolve`,{method:'POST'}); await route('benefits',false); return; }
  const af=e.target.closest('[data-advice-feedback]'); if(af){ await api('advice/feedback',{method:'POST',body:JSON.stringify({adviceKey:af.dataset.adviceFeedback,kind:af.dataset.adviceKind||'',useful:af.dataset.useful==='true'})}); toast('Спасибо — учтём'); return; }
  const mood=e.target.closest('[data-mood]'); if(mood){ await api('mood',{method:'POST',body:JSON.stringify({mood:mood.dataset.mood,energy:Number(mood.dataset.energy||3)})}); toast('Учтём настроение в советах'); await route('today',false); return; }
  if(e.target.closest('[data-voice-shopping]')){ startVoiceShopping(); return; }
  if(e.target.closest('[data-voice-stop]')){ try{window.__lifeShoppingRecognition?.stop()}catch{} return; }
  if(e.target.closest('[data-voice-add]')){ await addVoiceShopping(); return; }
  if(e.target.closest('[data-generate-day-image]')){ await generateDayImage(); return; }
  const lc=e.target.closest('[data-legal-cat]'); if(lc){ state.legalCategory=lc.dataset.legalCat; document.querySelectorAll('[data-legal-cat]').forEach(x=>x.classList.toggle('active',x.dataset.legalCat===state.legalCategory)); return; }
  if(e.target.closest('[data-legal-submit]')){ const text=document.querySelector('#legal-text').value.trim(); if(!text){toast('Опиши ситуацию');return;} const a=await api('legal/advice',{method:'POST',body:JSON.stringify({category:state.legalCategory,text})}); renderLegalResult(a); return; }
  if(e.target.closest('[data-logout]')){ await api('auth/logout',{method:'POST'}); state.me=null; showAuth(); return; }
  if(e.target.closest('[data-retry]')){ await route(state.view,false); return; }
});

document.addEventListener('submit',async e=>{
  const form=e.target;
  if(form.id==='login-form'){ e.preventDefault(); const x=Object.fromEntries(new FormData(form)); const err=document.querySelector('#login-error'); err.textContent=''; try{state.me=await api('auth/login',{method:'POST',body:JSON.stringify(x)});showApp();setProfileUI();await route('today');}catch(ex){err.textContent=ex.message==='unauthorized'?'Неверный email или пароль':ex.message;} return; }
  if(form.id==='register-form'){ e.preventDefault(); const x=Object.fromEntries(new FormData(form)); const err=document.querySelector('#register-error'); err.textContent=''; try{state.me=await api('auth/register',{method:'POST',body:JSON.stringify(x)});showApp();setProfileUI();await route('today');}catch(ex){err.textContent=ex.message;} return; }
  if(form.id==='entity-form'){ e.preventDefault(); try{await submitEntity(form)}catch(ex){toast(ex.message)} return; }
  if(form.id==='profile-form'){ e.preventDefault(); const x=Object.fromEntries(new FormData(form)); try{const p=await api('profile',{method:'PUT',body:JSON.stringify(x)});state.me.displayName=p.displayName;setProfileUI();toast('Профиль сохранён');}catch(ex){toast(ex.message)} return; }
});

document.querySelectorAll('[data-auth-tab]').forEach(btn=>btn.addEventListener('click',()=>{document.querySelectorAll('[data-auth-tab]').forEach(x=>x.classList.toggle('active',x===btn));document.querySelector('#login-form').classList.toggle('hidden',btn.dataset.authTab!=='login');document.querySelector('#register-form').classList.toggle('hidden',btn.dataset.authTab!=='register');}));
document.querySelector('#refresh-btn').addEventListener('click',()=>route(state.view,false));
window.addEventListener('hashchange',()=>{const v=location.hash.slice(1);if(titles[v]&&v!==state.view)route(v,false)});
bootstrap();
