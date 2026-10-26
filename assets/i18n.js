// Тексты сайта на трёх языках: ru — русский, en — английский, tg — таджикский.
// Ключи — из атрибутов data-t / data-t-attr в index.html и из main.js.
// {brand}, {author}, {version} подставляются из CONFIG в main.js.
// Внутри фигурных скобок — строгий JSON: его читает проверка `python tools/check_i18n.py`.
const T = {
"meta.title": {
  "ru": "{brand} — маленький остров наверху экрана Windows",
  "en": "{brand} — a little island at the top of your Windows screen",
  "tg": "{brand} — ҷазираи хурд дар болои экрани Windows"
},
"meta.desc": {
  "ru": "{brand} превращает верхний край экрана Windows в живой остров: музыка, тексты песен, таймер фокуса, вода, файлы и задачи — всё на расстоянии одного взгляда.",
  "en": "{brand} turns the top edge of Windows into a living island: music, lyrics, a focus timer, water, files and tasks, all one glance away.",
  "tg": "{brand} канори болоии экрани Windows-ро ба ҷазираи зинда табдил медиҳад: мусиқӣ, матни сурудҳо, таймери тамаркуз, об, файлҳо ва вазифаҳо — ҳамааш дар як нигоҳ."
},
"a11y.skip": {"ru": "Перейти к содержанию", "en": "Skip to content", "tg": "Гузаштан ба мундариҷа"},

"nav.label": {"ru": "Главное меню", "en": "Main menu", "tg": "Менюи асосӣ"},
"nav.features": {"ru": "Возможности", "en": "Features", "tg": "Имкониятҳо"},
"nav.principles": {"ru": "Принципы", "en": "Principles", "tg": "Принсипҳо"},
"nav.faq": {"ru": "Вопросы", "en": "FAQ", "tg": "Саволҳо"},
"nav.join": {"ru": "Записаться", "en": "Join waitlist", "tg": "Номнавис шудан"},
"nav.download": {"ru": "Скачать", "en": "Download", "tg": "Боргирӣ"},
"lang.label": {"ru": "Язык", "en": "Language", "tg": "Забон"},

"hero.badge": {"ru": "Скоро для Windows 10 и 11", "en": "Coming soon to Windows 10 and 11", "tg": "Ба зудӣ барои Windows 10 ва 11"},
"hero.badge.live": {"ru": "Уже доступно для Windows 10 и 11", "en": "Out now for Windows 10 and 11", "tg": "Акнун барои Windows 10 ва 11 дастрас аст"},
"hero.title": {"ru": "Остров наверху вашего экрана", "en": "An island at the top of your screen", "tg": "Ҷазира дар болои экрани шумо"},
"hero.lede": {
  "ru": "{brand} живёт у верхнего края Windows. Наведите курсор — остров раскроется и покажет музыку, таймер, напоминания и файлы. Уберите — и он снова станет тихой полоской.",
  "en": "{brand} lives at the top edge of Windows. Point at it and the island opens up with your music, timer, reminders and files. Move away and it shrinks back into a quiet strip.",
  "tg": "{brand} дар канори болоии Windows ҷой гирифтааст. Курсорро наздик баред — ҷазира кушода мешавад ва мусиқӣ, таймер, ёдраскуниҳо ва файлҳоро нишон медиҳад. Дур баред — боз ба рахи ороми хурд табдил меёбад."
},
"hero.download": {"ru": "Скачать для Windows", "en": "Download for Windows", "tg": "Барои Windows боргирӣ кунед"},
"hero.version": {"ru": "Версия {version}", "en": "Version {version}", "tg": "Версияи {version}"},

"wl.ph": {"ru": "Email или Telegram", "en": "Email or Telegram", "tg": "Email ё Telegram"},
"wl.label": {"ru": "Ваш email или Telegram", "en": "Your email or Telegram", "tg": "Email ё Telegram-и шумо"},
"wl.submit": {"ru": "Записаться", "en": "Join the waitlist", "tg": "Номнавис шудан"},
"wl.note": {"ru": "Напишем один раз — в день выхода. Без рассылок.", "en": "One message on launch day. No newsletters.", "tg": "Танҳо як бор менависем — дар рӯзи баромад. Бе хабарномаҳо."},
"wl.done": {
  "ru": "Готово! Вы в списке — напишем, как только {brand} выйдет.",
  "en": "You're on the list! We'll write as soon as {brand} is out.",
  "tg": "Тайёр! Шумо дар рӯйхат ҳастед — ҳамин ки {brand} барояд, хабар медиҳем."
},
"wl.err.empty": {"ru": "Укажите email или Telegram.", "en": "Enter your email or Telegram.", "tg": "Email ё Telegram-и худро нависед."},
"wl.err.bad": {
  "ru": "Похоже на опечатку: нужен email или ник в Telegram (@username).",
  "en": "That looks like a typo: use an email or a Telegram @username.",
  "tg": "Ба хато монанд аст: email ё номи Telegram (@username) лозим аст."
},
"form.sending": {"ru": "Отправляем…", "en": "Sending…", "tg": "Фиристода истодаем…"},
"form.fail": {"ru": "Не получилось отправить. Попробуйте ещё раз чуть позже.", "en": "Couldn't send. Please try again in a moment.", "tg": "Фиристода нашуд. Лутфан каме баъд боз кӯшиш кунед."},
"form.off": {"ru": "Форма ещё не подключена.", "en": "The form isn't connected yet.", "tg": "Форма ҳоло пайваст нашудааст."},

"demo.label": {"ru": "Демо острова {brand}", "en": "{brand} island demo", "tg": "Намоиши ҷазираи {brand}"},
"demo.chips": {"ru": "Состояния острова", "en": "Island states", "tg": "Ҳолатҳои ҷазира"},
"demo.idle": {"ru": "Покой", "en": "Rest", "tg": "Оромӣ"},
"demo.music": {"ru": "Музыка", "en": "Music", "tg": "Мусиқӣ"},
"demo.focus": {"ru": "Фокус", "en": "Focus", "tg": "Тамаркуз"},
"demo.water": {"ru": "Вода", "en": "Water", "tg": "Об"},
"demo.files": {"ru": "Файлы", "en": "Files", "tg": "Файлҳо"},
"demo.charge": {"ru": "Зарядка", "en": "Charging", "tg": "Барқгирӣ"},
"demo.water.amount": {"ru": "1,2 из 2 л", "en": "1.2 of 2 L", "tg": "1,2 аз 2 л"},
"demo.cap.idle": {"ru": "В покое — только время и тихая волна.", "en": "At rest: just the time and a gentle wave.", "tg": "Дар ҳолати оромӣ — танҳо вақт ва мавҷи сабук."},
"demo.cap.music": {"ru": "Что играет в любом плеере — с паузой и перемоткой.", "en": "Whatever's playing in any player, with pause and skip.", "tg": "Он чи дар ҳар плеер садо медиҳад — бо таваққуф ва гузариш."},
"demo.cap.focus": {"ru": "25 минут работы, потом короткий перерыв.", "en": "25 minutes of work, then a short break.", "tg": "25 дақиқа кор, баъд танаффуси кӯтоҳ."},
"demo.cap.water": {"ru": "Сколько выпито сегодня и сколько осталось до цели.", "en": "How much you've had today and how far to your goal.", "tg": "Имрӯз чӣ қадар нӯшидед ва то ҳадаф чӣ қадар монд."},
"demo.cap.files": {"ru": "Перетащите файл на остров — он подождёт вас там.", "en": "Drop a file on the island and it waits for you there.", "tg": "Файлро ба ҷазира кашед — он дар он ҷо интизори шумо мешавад."},
"demo.cap.charge": {"ru": "Подключили зарядку — остров коротко это подтвердит.", "en": "Plug in the charger and the island gives a quick nod.", "tg": "Барқгирро пайваст кардед — ҷазира кӯтоҳ тасдиқ мекунад."},

"feat.title": {"ru": "Маленький остров, много дел", "en": "A small island that does a lot", "tg": "Ҷазираи хурд, корҳои зиёд"},
"feat.lede": {
  "ru": "Ничего не нужно настраивать и закреплять: остров сам показывает то, что важно прямо сейчас.",
  "en": "Nothing to set up or pin: the island shows what matters right now on its own.",
  "tg": "Ҳеҷ чизро танзим кардан ё часпондан лозим нест: ҷазира худаш он чиро нишон медиҳад, ки ҳозир муҳим аст."
},
"f.music.t": {"ru": "Музыка из любого плеера", "en": "Music from any player", "tg": "Мусиқӣ аз ҳар плеер"},
"f.music.d": {
  "ru": "Spotify, YouTube в браузере и любой плеер, который видит Windows: название, обложка и управление — без переключения окон.",
  "en": "Spotify, YouTube in your browser and any player Windows can see: title, cover art and controls, without switching windows.",
  "tg": "Spotify, YouTube дар браузер ва ҳар плеере, ки Windows мебинад: ном, муқова ва идоракунӣ — бе гузаштан ба равзанаи дигар."
},
"f.lyrics.t": {"ru": "Тексты песен в такт", "en": "Lyrics in time", "tg": "Матни суруд ҳамқадам"},
"f.lyrics.d": {
  "ru": "Каждая строчка подсвечивается ровно тогда, когда её поют. Нажмите на строку — песня перемотается к ней.",
  "en": "Each line lights up right as it's sung. Tap a line and the song jumps there.",
  "tg": "Ҳар сатр маҳз ҳамон лаҳзае равшан мешавад, ки суруда мешавад. Ба сатр пахш кунед — суруд ба ҳамон ҷо мегузарад."
},
"f.focus.t": {"ru": "Таймер фокуса", "en": "Focus timer", "tg": "Таймери тамаркуз"},
"f.focus.d": {
  "ru": "Помодоро прямо в острове: обратный отсчёт всегда на виду, а мягкий сигнал подскажет, когда пора отдохнуть.",
  "en": "Pomodoro right inside the island: the countdown stays in view and a soft chime tells you when to rest.",
  "tg": "Помодоро дар худи ҷазира: ҳисоби баръакс ҳамеша дар пеши назар аст ва садои мулоим вақти истироҳатро хабар медиҳад."
},
"f.water.t": {"ru": "Вода по расписанию", "en": "Water on schedule", "tg": "Об аз рӯи ҷадвал"},
"f.water.d": {
  "ru": "Задайте цель на день и рабочие часы — {brand} напомнит сделать глоток и покажет, сколько уже выпито.",
  "en": "Set a daily goal and your hours. {brand} reminds you to take a sip and shows how much you've had.",
  "tg": "Ҳадафи рӯзона ва соатҳои кориро муқаррар кунед — {brand} нӯшидани обро ёдрас мекунад ва нишон медиҳад, ки чӣ қадар нӯшидаед."
},
"f.files.t": {"ru": "Полка для файлов", "en": "A shelf for files", "tg": "Рафи файлҳо"},
"f.files.d": {
  "ru": "Бросьте файл на остров, переключитесь в чат или почту и вытащите его туда. Сам файл остаётся на своём месте.",
  "en": "Drop a file on the island, switch to a chat or email and drag it out there. The file itself never moves.",
  "tg": "Файлро ба ҷазира партоед, ба чат ё почта гузаред ва онро ба он ҷо кашед. Худи файл дар ҷояш мемонад."
},
"f.tasks.t": {"ru": "Задачи с напоминанием", "en": "Tasks with reminders", "tg": "Вазифаҳо бо ёдраскунӣ"},
"f.tasks.d": {
  "ru": "Запишите дело и поставьте время — в нужную минуту остров мягко напомнит.",
  "en": "Write a task down, give it a time, and the island nudges you at the right minute.",
  "tg": "Корро нависед ва вақт гузоред — дар дақиқаи лозимӣ ҷазира оҳиста ёдрас мекунад."
},
"f.keys.t": {"ru": "Громкость, яркость, раскладка", "en": "Volume, brightness, keyboard", "tg": "Овоз, равшанӣ, клавиатура"},
"f.keys.d": {
  "ru": "Аккуратные индикаторы вместо стандартных и подсказка, когда включился Caps Lock или сменился язык ввода.",
  "en": "Neat indicators in place of the stock ones, plus a hint when Caps Lock turns on or the input language changes.",
  "tg": "Нишондиҳандаҳои зебо ба ҷои стандартӣ ва ишора, вақте ки Caps Lock фаъол шуд ё забони клавиатура иваз гардид."
},
"f.status.t": {"ru": "Батарея, сеть, наушники", "en": "Battery, network, earbuds", "tg": "Батарея, шабака, гӯшмонак"},
"f.status.d": {
  "ru": "Короткое уведомление, когда подключили зарядку, пропал Wi‑Fi или подключились наушники.",
  "en": "A quick heads-up when the charger goes in, Wi‑Fi drops or your earbuds connect.",
  "tg": "Огоҳии кӯтоҳ, вақте ки барқгир пайваст шуд, Wi‑Fi қатъ гардид ё гӯшмонак пайваст шуд."
},

"pr.title": {"ru": "Тихий, лёгкий и только ваш", "en": "Quiet, light and yours alone", "tg": "Ором, сабук ва танҳо аз они шумо"},
"pr.lede": {"ru": "Так мы делаем {brand} с первой версии.", "en": "How we're building {brand} from the first version.", "tg": "Мо {brand}-ро аз версияи аввал ҳамин тавр месозем."},
"pr1.t": {"ru": "Не мешает работать", "en": "Stays out of the way", "tg": "Ба кор халал намерасонад"},
"pr1.d": {
  "ru": "Пока ничего не происходит, остров не рисует и не нагружает процессор. Игры и фильмы во весь экран он не перекрывает.",
  "en": "When nothing is happening, the island doesn't draw or load the CPU. It never covers full-screen games and films.",
  "tg": "Вақте ки ҳеҷ чиз рӯй намедиҳад, ҷазира расм намекашад ва протсессорро бор намекунад. Бозиҳо ва филмҳои дар тамоми экран кушодаро намепӯшонад."
},
"pr2.t": {"ru": "Данные остаются у вас", "en": "Your data stays with you", "tg": "Маълумот назди шумо мемонад"},
"pr2.d": {
  "ru": "Задачи, вода и полка хранятся на вашем компьютере. Без аккаунтов, рекламы и слежки.",
  "en": "Tasks, water logs and the shelf live on your computer. No accounts, ads or tracking.",
  "tg": "Вазифаҳо, об ва раф дар компютери шумо нигоҳ дошта мешаванд. Бе аккаунт, реклама ва пайгирӣ."
},
"pr3.t": {"ru": "Родной для Windows", "en": "Native to Windows", "tg": "Барои Windows сохта шудааст"},
"pr3.d": {
  "ru": "Сделан под Windows 10 и 11: чёткий при любом масштабе экрана и на нескольких мониторах.",
  "en": "Made for Windows 10 and 11: crisp at any display scale and across several monitors.",
  "tg": "Барои Windows 10 ва 11: дар ҳар миқёси экран ва дар якчанд монитор равшан менамояд."
},

"wls.kicker": {"ru": "Лист ожидания", "en": "Waitlist", "tg": "Рӯйхати интизорӣ"},
"wls.title": {"ru": "Будьте первыми на острове", "en": "Be first on the island", "tg": "Аввалин шуда ба ҷазира оед"},
"wls.lede": {
  "ru": "Оставьте email или Telegram — пришлём ссылку в день выхода первой версии.",
  "en": "Leave your email or Telegram and we'll send the link the day the first version is out.",
  "tg": "Email ё Telegram-ро гузоред — дар рӯзи баромади версияи аввал пайвандро мефиристем."
},
"wls.title.live": {"ru": "Скачайте {brand}", "en": "Get {brand}", "tg": "{brand}-ро боргирӣ кунед"},
"wls.lede.live": {"ru": "Windows 10 или 11, 64 бита.", "en": "Windows 10 or 11, 64-bit.", "tg": "Windows 10 ё 11, 64-бит."},

"fb.kicker": {"ru": "Обратная связь", "en": "Feedback", "tg": "Фикру мулоҳиза"},
"fb.title": {"ru": "Чего не хватает?", "en": "What's missing?", "tg": "Чӣ намерасад?"},
"fb.lede": {
  "ru": "Расскажите, что вы хотели бы видеть в {brand}, или напишите о проблеме. Мы читаем каждое сообщение.",
  "en": "Tell us what you'd like to see in {brand}, or report a problem. We read every message.",
  "tg": "Бигӯед, ки дар {brand} чиро дидан мехоҳед, ё дар бораи мушкилот нависед. Мо ҳар паёмро мехонем."
},
"fb.msg.ph": {"ru": "Ваша идея или отзыв", "en": "Your idea or feedback", "tg": "Ғоя ё фикри шумо"},
"fb.contact.ph": {"ru": "Email или Telegram — если хотите ответ", "en": "Email or Telegram, if you'd like a reply", "tg": "Email ё Telegram — агар ҷавоб хоҳед"},
"fb.contact.label": {"ru": "Контакт, необязательно", "en": "Contact, optional", "tg": "Тамос, ихтиёрӣ"},
"fb.submit": {"ru": "Отправить", "en": "Send", "tg": "Фиристодан"},
"fb.ok": {"ru": "Спасибо! Сообщение дошло.", "en": "Thanks! Your message got through.", "tg": "Ташаккур! Паёми шумо расид."},
"fb.err.short": {"ru": "Напишите хотя бы пару слов.", "en": "Write at least a few words.", "tg": "Ақаллан чанд калима нависед."},

"faq.title": {"ru": "Вопросы", "en": "Questions", "tg": "Саволҳо"},
"faq.q1": {"ru": "Когда выйдет {brand}?", "en": "When is {brand} coming out?", "tg": "{brand} кай мебарояд?"},
"faq.a1": {
  "ru": "Мы собираем первую версию. Запишитесь в лист ожидания — напишем в тот же день, когда она будет готова.",
  "en": "We're building the first version now. Join the waitlist and we'll write the day it's ready.",
  "tg": "Мо ҳоло версияи аввалро месозем. Ба рӯйхати интизорӣ номнавис шавед — ҳамон рӯзе, ки тайёр шуд, менависем."
},
"faq.q2": {"ru": "Сколько это будет стоить?", "en": "How much will it cost?", "tg": "Нархаш чанд мешавад?"},
"faq.a2": {
  "ru": "Условия объявим до выхода, и первыми их узнают те, кто в листе ожидания.",
  "en": "We'll announce pricing before launch, and people on the waitlist will hear first.",
  "tg": "Шартҳоро пеш аз баромад эълон мекунем ва аввал онҳое мефаҳманд, ки дар рӯйхати интизорӣ ҳастанд."
},
"faq.q3": {"ru": "На каких компьютерах заработает?", "en": "Which PCs will it run on?", "tg": "Дар кадом компютерҳо кор мекунад?"},
"faq.a3": {"ru": "На Windows 10 и Windows 11, 64 бита.", "en": "Windows 10 and Windows 11, 64-bit.", "tg": "Дар Windows 10 ва Windows 11, 64-бит."},
"faq.q4": {"ru": "Какие плееры поддерживаются?", "en": "Which music players will work?", "tg": "Кадом плеерҳо дастгирӣ мешаванд?"},
"faq.a4": {
  "ru": "Любые, что появляются в системном управлении музыкой Windows: Spotify, браузеры с YouTube и Яндекс Музыкой, Windows Media Player и другие.",
  "en": "Any that show up in the Windows media controls: Spotify, browsers playing YouTube, Windows Media Player and more.",
  "tg": "Ҳар плеере, ки дар идоракунии мусиқии Windows пайдо мешавад: Spotify, браузерҳо бо YouTube ва Яндекс Мусиқӣ, Windows Media Player ва дигарон."
},
"faq.q5": {"ru": "Что будет с моими данными?", "en": "What happens to my data?", "tg": "Бо маълумоти ман чӣ мешавад?"},
"faq.a5": {
  "ru": "В приложении — ничего: всё хранится на вашем компьютере. С этого сайта к нам попадает только то, что вы сами отправили через формы, и мы используем это лишь для ответа и письма о выходе.",
  "en": "In the app, nothing: everything stays on your computer. This site only receives what you send through the forms, and we use it only to reply and to tell you about the launch.",
  "tg": "Дар барнома — ҳеҷ чиз: ҳама дар компютери шумо мемонад. Аз ин сайт ба мо танҳо он чизе мерасад, ки худатон тавассути формаҳо фиристодед ва мо онро танҳо барои ҷавоб ва хабари баромад истифода мебарем."
},
"faq.q6": {"ru": "Кто делает {brand}?", "en": "Who's behind {brand}?", "tg": "{brand}-ро кӣ месозад?"},
"faq.a6": {
  "ru": "{author}. Если есть вопрос, идея или хотите помочь — напишите через <a href=\"#feedback\">форму обратной связи</a>.",
  "en": "{author}. Got a question, an idea or want to help? Write through the <a href=\"#feedback\">feedback form</a>.",
  "tg": "{author}. Агар савол, ғоя ё хоҳиши кумак дошта бошед, тавассути <a href=\"#feedback\">формаи фикру мулоҳиза</a> нависед."
},

"foot.made": {"ru": "Сделано {author}", "en": "Made by {author}", "tg": "Сохтаи {author}"},
"foot.nav": {"ru": "Ссылки", "en": "Links", "tg": "Пайвандҳо"},
"foot.feedback": {"ru": "Написать нам", "en": "Contact us", "tg": "Ба мо нависед"},
"foot.privacy": {"ru": "Сайт не использует аналитику и cookie.", "en": "This site uses no analytics or cookies.", "tg": "Ин сайт аналитика ва cookie истифода намебарад."}
};
