# MisioCache — naprawa jednego testu CI

## Błąd
`test_byte_ranges_014.CleanSmartDownloadsTests.test_diagnostics_are_not_in_browser_or_config` oczekuje napisu `MisioCache — aktualizacje`. Ostatnia paczka przywróciła starszy tytuł `MisioCache — ustawienia`.

## Zmiana
Tylko plik `src/Jellyfin.Plugin.MisioCache/Web/configPage.html`: zamiana dwóch widocznych nagłówków (HTML `<title>` i `<h1>`) na `MisioCache — aktualizacje`, zgodnie z bieżącą nazwą menu. Nie dotyczy działania pobierania, przycisków, ustawień, wersji ani workflow.

## Wdrożenie
Rozpakuj pliki i prześlij zmieniony HTML we wskazane miejsce w repo `kumblip/MisioCache` na gałęzi `main` — najlepiej przez edycję pojedynczego pliku na GitHubie. Uruchomi się standardowy automatyczny workflow. Nie wgrywaj na nowo poprzedniego ZIP i nie edytuj `.github`, `meta.json` ani `manifest.json`.

W CI po tej zmianie test powinien zaakceptować napis `MisioCache — aktualizacje`. Automatyczna publikacja zależy od udanej kompilacji i pozostałych testów.
