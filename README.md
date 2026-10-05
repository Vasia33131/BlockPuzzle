Block Puzzle
Головоломка с блоками для мобильных и браузера. Выпущена на Яндекс Играх.

### [Block Puzzle](https://yandex.ru/games/app/556364) — [играть на Яндекс Играх](https://yandex.ru/games/app/556364) · [код](https://github.com/Vasia33131/BlockPuzzle)

<p align="left">
  <img src="https://github.com/user-attachments/assets/cc025153-3f28-4fac-9961-b699888da61f" width="30%" />
  <img src="https://github.com/user-attachments/assets/27ee46c6-9658-4926-8652-96337f87c496" width="30%" />
  <img src="https://github.com/user-attachments/assets/10ee84db-f37b-4e31-a6fc-9077ec0a6e0a" width="30%" />
</p> 

Об игре
Перетаскивайте фигуры на поле, заполняйте строки и столбцы и очищайте их, чтобы набрать очки. Игра заканчивается, когда ни одна фигура больше не помещается на поле.

Режимы: бесконечная игра и карта уровней с целями.
Бустеры и отмена хода.
Мета-прогрессия: уровень игрока, ежедневные задания и награды, магазин монет, темы оформления.
Туториал для новых игроков.
Портретная и альбомная ориентация, safe area.
Локализация: русский и английский.
Интеграция с Яндекс Играми
Возможность	Где в коде
Облачные сохранения	YandexCloudProgressService, YandexRunCloudStore
Лидерборд	YandexLeaderboardService
Реклама: rewarded, interstitial, sticky	YandexRewardedService, YandexInterstitialService, YandexStickyService
Внутриигровые покупки	YandexPaymentsService
Аналитика	YandexMetricaService
Язык, отзыв, ярлык на рабочем столе	YandexLanguageService, YandexReviewService, YandexShortcutService
Стек
Unity 2022.3 LTS · C# · uGUI + TextMeshPro · PluginYG2 (Yandex Games SDK) · WebGL, Android

Архитектура
Код лежит в Assets/_Game/Scripts и разбит на сборки (asmdef):

Сборка	Что внутри
Core	Данные и конфиги: фигуры, уровни, темы, прогресс игрока, локализация
Gameplay	Поле (GridModel без зависимостей от Unity и GridCellView только для отрисовки), фигуры, эффекты
Managers	Игровой цикл, счёт, бустеры, сохранение забега, буфер отмены, звук и музыка
UI	Панели, HUD, карта уровней, магазин, туториал
Bootstrap	Точка входа и сборка сцены
Platform	Сервисы Яндекс Игр
Editor	Редакторские утилиты для генерации контента и настройки проекта
Запуск
Откройте проект в Unity 2022.3.
Откройте игровую сцену и нажмите Play.
Автор
Василий · GitHub · vasilijzironkin2@gmail.com

