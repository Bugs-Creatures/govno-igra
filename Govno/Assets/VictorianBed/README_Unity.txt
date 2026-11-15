КРОВАТЬ ДЛЯ UNITY

ВЕРСИЯ 2: ткань, дерево и металл осветлены; шероховатость металла >= 0.50.
Без emission. При отсутствии освещения материал остаётся тёмным.
Для обновления скопируйте содержимое VictorianBed поверх существующей папки
Assets/VictorianBed с заменой файлов. НЕ удаляйте существующие .meta и Materials,
чтобы сохранить ссылки на модель и материалы в сценах.
Скрипт обновит материалы версии 1 при следующей компиляции.
Если нужно, вызовите Tools > Victorian Bed > Setup materials.

1. Распакуйте архив.
2. Перетащите ВСЮ папку VictorianBed в окно Project, внутрь Assets.
   Сохраните название папки VictorianBed. Не переносите только FBX.
3. Дождитесь импорта и компиляции скрипта из папки Editor.
4. Перетащите Victorian_Bed.fbx из Project в сцену.

Скрипт автоматически создаёт 5 материалов в Materials и связывает их с FBX.
Выбирает Standard, URP/Lit или HDRP/Lit по текущему render pipeline.
Если настройка не сработала или вы сменили pipeline:
Tools > Victorian Bed > Setup materials.
Для выполнения скрипта в проекте не должно быть ошибок компиляции.
После создания материалов папку Editor можно удалить; материалы сохранятся.

Карты: BaseColor (sRGB), Normal (Normal Map), MetallicSmoothness (linear).
MetallicSmoothness: R = metallic, G = 1 (AO), B = 1, A = 1 - roughness.
Это также совместимая Mask Map для HDRP без дополнительной детализации.
Тонирование из Blender уже внесено в BaseColor; цвет материала = белый.
Масштаб UV назначается скриптом отдельно для каждого материала.
В FBX встроены карты цвета; остальные PBR-карты находятся в Textures.
Сам FBX не переносит произвольные шейдерные узлы Blender, поэтому нужен комплект.

Геометрия: 5 184 треугольника, 56 mesh-объектов, 5 материалов.
Размер матраса 1.60 x 2.00 м. Экспорт в метрах, Y-up.
Без студии, света, камеры и одеяла. Коллайдеры не добавлены.
FBX проверен повторным импортом в Blender: геометрия, UV, материалы и размеры.
Автонастройка материалов в Unity Editor в этой сессии не запускалась.

Источник PBR-текстур: ambientCG, лицензия CC0.
https://ambientcg.com/view?id=Metal063
https://ambientcg.com/view?id=Wood039
https://ambientcg.com/view?id=Fabric030
https://docs.ambientcg.com/license/

Unity: metallic/smoothness convention and material remapping:
https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader
https://docs.unity.com/en-us/engine/7000.0/script-reference/unityeditor/assetimporter/addremap
