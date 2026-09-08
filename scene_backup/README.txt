Резервные копии сцены Demo.unity (создано при починке 8 сентября 2026)

Demo_broken_fcc8945.unity   - сломанная версия, которая пришла с pull'ом (630 блоков,
                              5 дублей fileID, 118 битых ссылок). Не открывается.
Demo_safe_50c3e908.unity    - последняя целая версия из main до всех сломанных мержей
                              (481 блок, 8 сентября 22:28). Гарантированно рабочая,
                              но без работы Outside / strelbis / Locomotion.
Demo_strelbis_00ffab20.unity- ветка strelbis, коммит "Boxxxxxxx" (733 блока, целая).

Сейчас в Assets/Scenes/Demo.unity лежит результат ручного трёхстороннего слияния
(759 блоков) веток Outside + strelbis + PlayerControl поверх 50c3e908.
Если Unity вдруг откажется её открывать - подставьте Demo_safe_50c3e908.unity.
