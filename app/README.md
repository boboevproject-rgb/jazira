# Jazira для Windows — приложение

Маленький «остров» у верхнего края экрана Windows 10 и 11. В покое — время, заряд или идущий таймер.
Наведите курсор — остров раскроется; уберите — свернётся обратно. Версия **0.1.0**.

[English below ↓](#english)

## Что умеет 0.1

- **Музыка из любого плеера**, который видит Windows (Spotify, YouTube и Яндекс Музыка в браузере,
  Media Player…): название, исполнитель, обложка, прогресс, пауза, назад и вперёд.
- **Таймер фокуса** 25/5 минут: идёт прямо в свёрнутом острове, в конце — мягкий сигнал.
- **Вода:** счётчик за день, цель 1,5–3 л, напоминание раз в час с 9:00 до 21:00, если давно не пили.
- **Короткие уведомления:** подключили или отключили зарядку, началась другая песня, пора пить воду,
  закончился фокус.
- Значок в трее: показать или спрятать остров, автозапуск с Windows, цель по воде, язык RU/EN/TJ, выход.
- Прячется, когда игра, фильм или презентация открыты во весь экран.
- Страницы раскрытого острова листаются стрелками по краям или колёсиком мыши.

Данные (вода, настройки) хранятся только на компьютере: `%APPDATA%\Jazira\state.json`.
В покое приложение почти не нагружает процессор (около 0,3 % одного ядра), памяти занимает ~130 МБ.

## Установка

1. Скачайте `Jazira-0.1.0-win-x64.zip` со [страницы релиза](https://github.com/boboevproject-rgb/jazira/releases/tag/v0.1.0) и распакуйте.
2. Запустите `install.bat` — права администратора не нужны. Jazira встанет в
   `%LOCALAPPDATA%\Programs\Jazira`, появятся ярлыки в «Пуске» и на рабочем столе.
3. Windows может предупредить «Защитник Windows предотвратил запуск» — приложение пока без цифровой
   подписи. Нажмите **Подробнее → Выполнить в любом случае**.

Повторный запуск `install.bat` из новой версии обновит программу, настройки останутся.
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

### Проверка вёрстки без экрана

```bash
Jazira.exe --snapshot <папка>
```

Рисует остров во всех состояниях на русском и таджикском (покой, музыка, фокус, вода, уведомления)
в PNG с масштабом 200 % и выходит. На экране ничего не появляется, плеер не нужен.

## Устройство

```
Jazira/App.xaml.cs          запуск, один экземпляр, журнал ошибок
Jazira/IslandWindow.xaml    вёрстка острова: покой, музыка, фокус, вода, уведомление
Jazira/IslandWindow.xaml.cs раскрытие по наведению, страницы, уведомления, обновление данных
Jazira/MediaService.cs      что играет — системный медиа-пульт Windows
Jazira/PowerService.cs      батарея и зарядка
Jazira/FocusTimer.cs        помодоро
Jazira/AppState.cs          настройки и журнал воды
Jazira/Strings.cs           тексты на ru / en / tg
Jazira/TrayIcon.cs          меню в трее
Jazira/Platform.cs          стиль окна, полноэкранные приложения, автозапуск, сигнал
Jazira/Snapshot.cs          режим --snapshot
install.bat, uninstall.bat  установка и удаление для текущего пользователя
```

## Дальше

Тексты песен в такт, полка для файлов, задачи с напоминаниями, индикаторы громкости, яркости и
раскладки, узкий режим для ноутбуков с толстой рамкой, выбор монитора, меньший размер файла.

---

<a id="english"></a>

## English

A small island at the top of the Windows 10/11 screen. Hover it to open music controls (any player
Windows can see), a 25/5 focus timer and a daily water counter with hourly reminders; it also shows
short notifications for the charger, track changes, water and the end of a focus session.
Tray menu: show/hide, start with Windows, water goal, language (RU/EN/TJ), exit.

**Install:** download `Jazira-0.1.0-win-x64.zip` from the [release page](https://github.com/boboevproject-rgb/jazira/releases/tag/v0.1.0), unzip it and run `install.bat` (no admin rights). The app is not
code-signed yet, so SmartScreen may warn you: *More info → Run anyway*. `uninstall.bat` removes it.

**Build:** .NET 9 SDK, `dotnet build Jazira/Jazira.csproj -c Release`; see the publish command above for
a single self-contained `Jazira.exe`. `Jazira.exe --snapshot <dir>` renders every island state to PNG.
