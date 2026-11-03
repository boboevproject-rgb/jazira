# Jazira для Windows — приложение

Маленький «остров» у верхнего края экрана Windows 10 и 11. В покое на нём время, заряд, идущий таймер
или строчка песни. Наведите курсор — остров раскроется, уберите — свернётся обратно. Версия **0.2.0**.

[English below ↓](#english)

## Что нового в 0.2

- **Вид на выбор:** пять тем («Ночь», «Стекло», «Лагуна», «Закат», «Светлая»). Форма — капсула
  или вырез, прилегающий к краю экрана. Рамка может подсвечиваться цветом обложки песни.
- **Прикрепить или отцепить.** Остров можно перетащить мышью в любое место экрана. Если отпустить
  его у верхнего края, он снова прикрепится. Можно выбрать монитор и размер от 80 до 150 %.
- **Полка для файлов.** Перетащите файлы на остров, и до шести из них полежат на полке. Потом их
  можно вытащить в папку, письмо или мессенджер, двойной щелчок открывает файл.
- **Тексты песен в такт** из открытой базы [LRCLIB](https://lrclib.net). Текущая строка видна
  в свёрнутом острове, в раскрытом — две строки. Понимает и названия вида
  «Исполнитель - Песня (Official Video)» из браузера.
- **Живой остров:** пружинистая анимация, эквалайзер по настоящему звуку из колонок.
- **Рыбка-кои** плавает в свёрнутом острове. На странице воды у неё пруд: чем дольше вы не пили,
  тем мельче вода и тем вялее рыбка, а после стакана воды — всплеск и пузырьки. Когда за компьютером
  никого нет, рыбка спит и не тратит процессор.
- **Индикаторы:** громкость, яркость (на ноутбуке), раскладка клавиатуры, Caps Lock. Цветные точки
  в острове показывают, что камера или микрофон сейчас заняты, — Jazira только читает журнал Windows
  и сама их не открывает.
- **Страница «Сегодня»:**
  - погода в двенадцати городах: Душанбе, Худжанд, Бохтар, Куляб, Хорог, Истаравшан, Пенджикент,
    Турсунзаде, Исфара, Вахдат, Москва и Санкт-Петербург;
  - официальный курс сомони по данным Нацбанка Таджикистана;
  - по желанию — время намаза с напоминанием. Оно выключено по умолчанию и рассчитывается
    по формулам: угол 18° для фаджра, 17° для иша, аср по ханафитскому мазхабу. Сверяйте время
    с вашей мечетью.
- **Автообновление.** Jazira сама находит новую версию в релизах GitHub, скачивает её, сверяет
  контрольную сумму SHA-256 и предлагает «Обновить». Обновление ставится в один клик, настройки
  сохраняются.
- **Правый клик по острову** открывает то же меню, что и значок в трее.

## Что было с 0.1

- **Музыка из любого плеера**, который видит Windows (Spotify, YouTube и Яндекс Музыка в браузере,
  Media Player…): название, исполнитель, обложка, прогресс, пауза, назад и вперёд.
- **Таймер фокуса** 25/5 минут: идёт прямо в свёрнутом острове, в конце — мягкий сигнал.
- **Вода:** счётчик за день, цель от 1,5 до 3 л, напоминание раз в час с 9:00 до 21:00, если вы давно
  не пили.
- **Короткие уведомления:** подключили или отключили зарядку, началась другая песня, пора пить воду,
  закончился фокус.
- Языки — русский, английский, таджикский. Остров прячется, когда игра, фильм или презентация открыты
  во весь экран.

## Куда приложение обращается в интернете

Ни ключей, ни аккаунтов. О вас ничего не отправляется — только то, что нужно для ответа:

| Сервис | Что уходит | Зачем |
|---|---|---|
| lrclib.net | название песни, исполнитель, длительность | тексты песен; можно выключить |
| api.open-meteo.com | координаты выбранного города | погода; можно выключить вместе со страницей «Сегодня» |
| nbt.tj | дата | курс сомони |
| api.github.com, github.com | ничего, кроме запроса | проверка и скачивание обновлений; можно выключить |

Настройки, журнал воды и полка хранятся только на компьютере: `%APPDATA%\Jazira\state.json`.

Нагрузка (замер на ноутбуке автора, остров свёрнут):

| Вариант | Процессор | Память |
|---|---|---|
| Пока вы работаете, рыбка включена | около 1,5 % одного ядра | около 285 МБ |
| Рыбка выключена | около 0,9 % | около 230 МБ, как у 0.1 |

## Установка

1. Скачайте `Jazira-<версия>-win-x64.zip` со [страницы релизов](https://github.com/boboevproject-rgb/jazira/releases)
   и распакуйте.
2. Запустите `install.bat` — права администратора не нужны. Jazira встанет в
   `%LOCALAPPDATA%\Programs\Jazira`, появятся ярлыки в «Пуске» и на рабочем столе.
3. Windows может предупредить «Защитник Windows предотвратил запуск» — приложение пока без цифровой
   подписи. Нажмите **Подробнее → Выполнить в любом случае**.

Версия 0.1 обновлять себя не умеет, поэтому 0.2 ставится поверх неё тем же `install.bat`, настройки
сохранятся. Дальше Jazira будет обновляться сама.
Удаление — `uninstall.bat` (настройки в `%APPDATA%\Jazira` сохраняются).

## Сборка из исходников

Нужен [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet build Jazira/Jazira.csproj -c Release
```

Один самодостаточный `Jazira.exe` (работает без установленного .NET):

```bash
dotnet publish Jazira/Jazira.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

### Выпуск релиза, который подхватит автообновление

- Тег — `vX.Y.Z`. Черновики приложение не видит.
- Архив называется `Jazira-X.Y.Z-win-x64.zip`, в корне архива лежит `Jazira.exe`, рядом —
  `install.bat` и `uninstall.bat`.
- Рядом с архивом выкладывается файл `Jazira-X.Y.Z-win-x64.zip.sha256` с контрольной суммой.
  Сумму можно и просто вписать в описание релиза. Без суммы приложение релиз пропустит.

```powershell
$z = "Jazira-0.2.0-win-x64.zip"; "$((Get-FileHash $z -Algorithm SHA256).Hash.ToLower())  $z" | Set-Content "$z.sha256" -NoNewline
```

### Проверка без экрана

```bash
Jazira.exe --snapshot <папка>
```

Рисует остров во всех состояниях в PNG с масштабом 200 % и выходит; на экране ничего не появляется,
плеер не нужен. Это 63 картинки: русский и таджикский язык, все темы, вырез, полка, тексты, рыбка,
страница «Сегодня», уведомления. Рядом кладёт `selftest.txt` с временем намаза для всех городов
и проверкой разбора текста песни.

## Устройство

```
Jazira/App.xaml.cs               запуск, один экземпляр, автообновление, журнал ошибок
Jazira/IslandWindow.xaml         вёрстка острова: покой, музыка, полка, фокус, вода, «Сегодня», уведомление
Jazira/IslandWindow.xaml.cs      раскрытие по наведению, страницы, уведомления, данные
Jazira/IslandWindow.Place.cs     место на экране, перетаскивание, монитор, масштаб, темы и подсветка
Jazira/IslandWindow.Shelf.cs     полка для файлов: перетаскивание внутрь и наружу
Jazira/IslandWindow.Live.cs      тексты, эквалайзер, индикаторы, «Сегодня», намаз, режим снимков
Jazira/Theme.cs                  пять тем
Jazira/Motion.cs                 пружинная анимация, цвет обложки
Jazira/KoiPond.cs                рыбка-кои и пруд, нарисованные кодом
Jazira/MediaService.cs           что играет — системный медиа-пульт Windows
Jazira/LyricsService.cs          тексты песен из LRCLIB
Jazira/CoreAudio.cs              громкость и уровень звука (Core Audio)
Jazira/SystemWatch.cs            раскладка, Caps Lock, камера и микрофон, яркость
Jazira/LocalInfo.cs              города, погода, курс сомони, расчёт времени намаза
Jazira/Updater.cs                проверка, скачивание и установка обновлений
Jazira/PowerService.cs           батарея и зарядка
Jazira/FocusTimer.cs             помодоро
Jazira/AppState.cs               настройки, журнал воды, полка
Jazira/Strings.cs                тексты на ru / en / tg
Jazira/TrayIcon.cs               меню в трее и по правому клику
Jazira/Platform.cs               стиль окна, полноэкранные приложения, автозапуск, сигнал
Jazira/Snapshot.cs               режим --snapshot
install.bat, uninstall.bat       установка и удаление для текущего пользователя
```

## Дальше

- Задачи с напоминаниями.
- Установка через winget.
- Цифровая подпись.
- Меньше размер файла и памяти.
- Проверка таджикских текстов носителем языка.

---

<a id="english"></a>

## English

A small island at the top of the Windows 10/11 screen.

**New in 0.2:**
- five themes and a capsule or edge-notch shape;
- detach the island and drag it anywhere, drop it at the top edge to pin it back;
- choice of monitor and size;
- a file shelf you can drag files into and out of;
- synced lyrics from LRCLIB;
- a springy "living" island with a real-audio equalizer and cover-colour glow;
- a koi fish whose pond tracks your water intake;
- indicators for volume, brightness, keyboard layout, Caps Lock, camera and microphone;
- a "Today" page with weather for 12 cities, official somoni rates from the National Bank of
  Tajikistan, and optional prayer times (off by default);
- SHA-256-verified auto-update from GitHub releases;
- right-click the island for the tray menu.

**Also from 0.1:**
- music controls for any player Windows can see;
- a 25/5 focus timer;
- a daily water counter;
- short notifications;
- RU/EN/TJ languages.

**Network:**
- lrclib.net (song title, artist and duration);
- open-meteo.com (city coordinates);
- nbt.tj (date);
- GitHub (updates).

No accounts or keys. Settings stay in `%APPDATA%\Jazira`.

**Install:** download `Jazira-<version>-win-x64.zip` from the [releases page](https://github.com/boboevproject-rgb/jazira/releases), unzip it and run `install.bat` (no admin rights). The app is not code-signed yet, so
SmartScreen may warn you: *More info → Run anyway*. 0.1 cannot update itself, so install 0.2 over it the
same way. From 0.2 on, Jazira updates itself.

**Releases for auto-update:**
- tag `vX.Y.Z`;
- asset `Jazira-X.Y.Z-win-x64.zip` with `Jazira.exe` at the root;
- `Jazira-X.Y.Z-win-x64.zip.sha256` next to it, or the hash in the release notes.

**Build:** .NET 9 SDK, `dotnet build Jazira/Jazira.csproj -c Release`; see the publish command above for
a single self-contained `Jazira.exe`. `Jazira.exe --snapshot <dir>` renders every island state to PNG.
