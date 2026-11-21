# MisioCache — osobna wtyczka Jellyfin 12.4

**Pierwsze wydanie:** `0.1.0.0` — instalowalna baza diagnostyczna. Nie jest to jeszcze aktywny cache wideo.

MisioCache nie modyfikuje MisioFlixa, Jellyfin Web, ani obecnego odtwarzacza. W panelu administratora Jellyfin pojawiają się dwa wpisy: **MisioCache** (widok ogólny) oraz **MisioCache — ustawienia** (konfiguracja i diagnostyka urządzenia). Zapisywana jest przyszła polityka cache; żadna polityka nie jest wykonywana w 0.1.

## Repozytorium

https://github.com/kumblip/MisioCache

## Szybki start: puste repozytorium GitHub

1. Otwórz repozytorium `kumblip/MisioCache` i wybierz **uploading an existing file**.
2. Rozpakuj ZIP źródłowy i prześlij **zawartość** folderu (pliki i foldery bez dodatkowego katalogu nadrzędnego). **Ważne:** GitHub przez stronę może pomijać pliki i foldery ukryte; sprawdź, czy w repozytorium znalazły się `.github/workflows/build.yml` i `.github/workflows/release.yml`. Jeśli nie, użyj GitHub Desktop lub `git` zgodnie z `WDROZENIE_GITHUB.md`.
3. W repo: **Settings → Actions → General → Workflow permissions → Read and write permissions → Save**. Dla aktywnych zabezpieczeń gałęzi `main` nadaj botowi możliwość zapisu lub dostosuj reguły tak, by publikacja manifestu była możliwa. GitHub może mieć odgórną blokadę tej opcji — wtedy workflow zgłosi brak uprawnień.
4. Commit na `main` uruchamia automatycznie **MisioCache — automatyczny Build i Release**: testy → .NET 10 → paczka → tag → Release → aktualizacja `manifest.json`.
5. Po pierwszym udanym Release dodaj katalog w **Jellyfin → Dashboard → Plugins → Repositories**:
   `https://raw.githubusercontent.com/kumblip/MisioCache/main/manifest.json`.
6. W katalogu Jellyfin zainstaluj MisioCache; uruchom ponownie serwer Jellyfin. Można też pobrać gotowe `MisioCache_0.1.0.0_JF12.4.zip` z **Releases** i ręcznie zainstalować, zgodnie z mechanizmem hosta.

**Numeracja automatyczna:** pierwszy release `0.1.0.0`, po kolejnej zmianie kodu `0.1.1.0`, następnie `0.1.2.0`, itd. Nie edytuj ręcznie wersji. Workflow sam synchronizuje `meta.json` oraz `.csproj`. Zmiany wyłącznie w README lub manifest nie tworzą kolejnego wydania. Release nie powstaje, jeśli testy albo kompilacja zakończą się błędem.

## Bezpieczeństwo i ograniczenia

- Plugin zapisuje preferencje po stronie NAS-a, ale **nie przechwytuje strumieni**.
- `CacheStorage`, `ServiceWorker` i `navigator.storage.estimate()` w panelu badają wyłącznie możliwości widoku administratora. Nie oznacza to obsługi cache przez natywny odtwarzacz Android/iOS.
- Buforowanie pełnego filmu po stronie urządzenia wymaga osobnej, zgodnej integracji klienta — bez zamiany obecnego playera.
- Technologia z aplikacji JellyPlay (Media3 SimpleCache) jest punktem odniesienia; nie kopiowano ani nie dołączono kodu JellyPlay.

## Budowanie lokalne

Wymagany SDK .NET 10 i dostęp do NuGet Jellyfin 12.4:

```sh
python3 tools/run_tests.py
dotnet publish src/Jellyfin.Plugin.MisioCache/Jellyfin.Plugin.MisioCache.csproj --configuration Release --output build/publish
python3 tools/package.py
```

W GitHub Actions build jest wykonywany automatycznie. Zobacz `WDROZENIE_GITHUB.md` i `PLAN_DALSZEGO_ROZWOJU.md`.
