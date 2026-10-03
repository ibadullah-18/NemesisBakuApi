# Bakı metro koordinatları

Mənbə: Bakı Metropoliteni / IDDA rəsmi açıq məlumat portalı.

- Dataset: https://opendata.az/en/@baki-metropoliteni/metro-cixislari-uzre-koordinatlar
- CSV: https://admin.opendata.az/dataset/3108fb32-8483-4126-8283-17f2f1452548/resource/6540e9a6-97d5-41c6-a05a-3365ed69805a/download/pathways.csv
- Mənbə yenilənməsi: 10 fevral 2026. Endirilmə: 3 oktyabr 2026.
- Rəsmi stansiya sayı: https://metro.gov.az/az/page/haqqimizda/baki-metropoliteni-haqqinda (27).

`pathways.csv` orijinal fayldır. `MetroStationSeed` hər stansiyanın 1-ci çıxışını
götürür; tam ədəd mikro-dərəcələr 1 000 000-a bölünür. CSV-də tarix kimi göstərilən
`28.05.2026 00:00:00` adı ingilis sütunundakı `28 May` ilə düzəldilib.
Memar Əcəmi və Memar Əcəmi 2 ayrıca stansiyalardır.

Mənbə koordinatları təxmini çıxış nöqtələri kimi təsvir edir. Bunlar yol marşrutu
deyil. Tarif stansiyanın saxlanmış nöqtəsinə düz xətt məsafəsinə əsaslanır.
Admin koordinatı, adı, ünvanı və aktivliyi dəyişə bilər. Migration ilkin məlumatı
bir dəfə əlavə edir; tətbiqin yenidən başlaması admin dəyişikliklərini əvəz etmir.

0–1 km (1 daxil) = 6 AZN; 1–2 km (2 daxil) = 7 AZN; >2 km = əvvəlki mağaza
kilometraj qaydası. Müqayisə yuvarlaqlaşdırılmamış məsafə ilə edilir. Aktiv metro
qalmasa ünvan çatdırılması mağaza qaydasına keçir, metroda təhvil isə mümkün olmur.
Metroda təhvil yalnız seçilmiş aktiv stansiya üçün 4 AZN-dir. Mağazadan götürmə 0 AZN.

Sifariş metro adını, məsafəni, qiyməti və qaydanı ayrıca saxlayır; adminin sonrakı
dəyişiklikləri keçmiş sifarişin qiymətinə təsir etmir. Mövcud DeliveryDistanceKm
həmişə mağaza məsafəsidir; MetroDistanceKm ayrı saxlanılır.
