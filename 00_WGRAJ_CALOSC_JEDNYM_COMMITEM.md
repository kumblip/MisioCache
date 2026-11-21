# MisioCache — aktualizacje i poprawione testy w JEDNYM WDROZENIU

To jest wspólna, kumulatywna poprawka: wcześniejsza pełna sekcja "Aktualizacje MisioCache" + poprawiony test manifestu dla zmieniających się wersji.

## Wgraj do GitHuba

1. Rozpakuj ZIP (łącznie z podkatalogami).
2. Wgraj WSZYSTKIE pliki ZIP do głównego katalogu `kumblip/MisioCache` (zastąp istniejące pliki o tej samej ścieżce).
3. **Nie zmieniaj / nie usuwaj** `manifest.json`, `meta.json`, pliku `.csproj` ani `.github/workflows` — nie znajdują się w tej paczce.
4. Zatwierdź jeden commit na `main`; standardowy workflow sam nada kolejny numer, wykona testy, opublikuje Release i zaktualizuje manifest.
5. Zainstaluj nową wersję przez katalog Jellyfina (po wymaganym restarcie zobaczysz przyciski).

## Co zawiera

* Cały mechanizm wersji w panelu: zainstalowana, GitHub, manifest, natywny katalog Jellyfina.
* Przycisk "Wymuś aktualizację katalogu Jellyfina" — odczytuje i porównuje wszystkie źródła, zapisuje zweryfikowany manifest do konfiguracji katalogu natywnego, następnie sprawdza wynik. **Nie instaluje DLL**.
* Przycisk "Zainstaluj aktualizację" — tylko dla zgodnej, nowszej wersji i po akcji administratora.
* Automatyczne sprawdzanie co 15 minut (bez instalowania).
* Poprawiony test `test_manifest_checksum_and_url` sprawdzający bieżącą wersję z `meta.json` oraz `0.1.1.0` i `0.1.2.0`.

## Czego nie robi

* Nie zmienia systemu automatycznego numerowania wersji ani GitHub Actions.
* Nie nadpisuje historii `manifest.json`.
* Nie wymienia odtwarzacza Jellyfina i nie uruchamia jeszcze cache wideo.
* Nie instaluje bez potwierdzenia aktualizacji DLL.
