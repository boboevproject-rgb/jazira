# Jazira в winget

Здесь манифесты для каталога [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) —
после того как их примут, Jazira ставится одной командой:

```bash
winget install Bilol.Jazira
```

Пакет портативный: winget распаковывает `Jazira.exe` в свою папку и добавляет команду `jazira`.
Ярлыков в «Пуске» winget не создаёт — после установки Jazira запускают один раз командой `jazira`,
а автозапуск включают в меню значка у часов. В такой установке Jazira сама не обновляется
(обновляет `winget upgrade`), иначе winget запутается в версиях.

## Подать версию

Манифест ссылается на архив релиза, поэтому подавать его можно только после публикации релиза
`vX.Y.Z` с файлом `Jazira-X.Y.Z-win-x64.zip`.

1. Сверить сумму: `InstallerSha256` в `Bilol.Jazira.installer.yaml` должна совпасть с
   `Jazira-X.Y.Z-win-x64.zip.sha256` из релиза. Если архив пересобирали — обновить её и `ReleaseDate`.
2. Проверить манифест:

   ```bash
   winget validate --manifest app/winget/manifests/b/Bilol/Jazira/0.3.0
   ```

3. Подать: форк microsoft/winget-pkgs → скопировать папку `manifests/b/Bilol/Jazira/0.3.0` по тому же
   пути → пулл-реквест. Робот каталога сам скачает архив, сверит сумму и проверит установку
   в чистой Windows; потом пакет смотрит модератор (обычно от нескольких дней до пары недель).

Следующие версии удобно подавать утилитой Microsoft
[wingetcreate](https://github.com/microsoft/winget-create) (нужен токен GitHub):

```bash
wingetcreate update Bilol.Jazira --version 0.4.0 --urls https://github.com/boboevproject-rgb/jazira/releases/download/v0.4.0/Jazira-0.4.0-win-x64.zip --submit
```

Проверено 4 ноября 2026: `winget validate` проходит без ошибок и предупреждений, имя `Bilol.Jazira`
и слово `jazira` в каталоге свободны. Установка из манифеста не проверялась — архива по ссылке
ещё нет, он появится вместе с релизом.
