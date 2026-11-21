## MisioCache v0.1.3.0 — obserwacja rzeczywistego ruchu (bez cache)

Ten etap dodaje **weryfikowalny, pasywny** pomiar żądań HTTP Range / odpowiedzi 206
oraz odczyt aktywnych sesji `/Sessions` z natywnego API Jellyfina.
Przydatne także przy zwykłym HTTP. Nie wymaga Service Worker ani HTTPS.

- **DOMYŚLNIE WYŁĄCZONY** licznik wideo HTTP można włączyć w panelu MisioCache.
- Nie przechwytuje ani nie podmienia strumienia, nie czyta bajtów filmu i nie pobiera go w tle.
- Zlicza tylko `GET`/`HEAD` tras `/Videos/<GUID>/...`, a nie zdjęcia, napisy ani logi.
- 30-minutowe okno, najwyżej 2048 próbek w pamięci RAM; nie zapisuje do pliku.
- Zachowuje tylko typ (plik, playlista, segment), informację o Range, status odpowiedzi
  i obecność `Content-Range`. Nie zapisuje adresów URL, zapytań, tokenów, tytułów ani ID.
- `HTTP 206` **nie dowodzi** istnienia cache po stronie urządzenia; zliczanie żądań
  **nie jest** pomiarem faktycznie przesłanych bajtów.
- Nazwa filmu i klient w podglądzie sesji są odczytywane na żądanie z natywnego `/Sessions`
  tylko przez administratora; nie trafiają do magazynu próbek.
- Nie wymienia odtwarzacza Jellyfin, nie modyfikuje MisioFlixa i jego integracji.

Instalacja: prześlij paczkę zmian do istniejącego repo `kumblip/MisioCache`
(z zachowaniem podfolderów). Nie podmieniaj istniejącego `manifest.json`.
Wybierz `Commit changes` na `main` i sprawdź Actions i Release. Po instalacji/restartcie
Jellyfina w panelu zobaczysz „Rzeczywiste odtwarzanie i żądania HTTP”. Włącz obserwację
oraz odtwórz materiał na urządzeniu. Kliknij „Sprawdź odtwarzanie i ruch”.

**Wersjonowanie:** źródła są oznaczone jako v0.1.3.0. Automatyczny workflow może wybrać
nowszy numer, jeśli w repo istnieje już wydany tag v0.1.3.0 lub późniejszy.

### Cel dalszej wersji

Na podstawie diagnostyki sprawdzić Direct Play, Direct Stream i transkodowanie,
wyznaczyć zgodną ścieżkę dla klienta i dopiero wtedy zdecydować o mechanizmie
przechowywania i ponownego wykorzystania danych na urządzeniu. Żadnej ingerencji
w istniejący odtwarzacz bez dowodu kompatybilności.

---

# MisioCache — osobna wtyczka Jellyfin 12.4

**Wersja bazowa:** `0.1.0.0` — instalowalna baza diagnostyczna. Nie jest to jeszcze aktywny cache wideo.

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

## Wymuszanie aktualizacji katalogu Jellyfina (od pierwszej aktualizacji)

W panelu **MisioCache — ustawienia → Aktualizacje MisioCache** można porównać 4 wersje: lokalną, GitHub Release, manifest i katalog Jellyfina. **Wymuś aktualizację katalogu Jellyfina** odczytuje commit `main`, weryfikuje immutable manifest przez natywny `IInstallationManager.GetPackages` Jellyfina i aktualizuje wyłącznie URL własnego repozytorium w konfiguracji serwera. Nie nadpisuje DLL i nie wykonuje instalacji. **Zainstaluj aktualizację** uruchamia natywny `POST /Packages/Installed/MisioCache` po potwierdzeniu administratora, wyłącznie dla zgodnego ABI 12.4.0.0 i nowej wersji obecnej w katalogu. Po instalacji Jellyfin może wymagać restartu.

Przed użyciem przycisku dodaj **jedno aktywne** repozytorium o URL `https://raw.githubusercontent.com/kumblip/MisioCache/main/manifest.json` w `Dashboard → Wtyczki → Repozytoria`. Funkcja może potem zmienić jego URL na rewizję SHA; jest to celowe i nie należy dodawać drugiej kopii. Aktualizacja sprawdza status co 15 minut, a instalacja zawsze wymaga decyzji administratora. Wtyczka pozostaje diagnostyczna; nadal nie przechwytuje strumienia wideo.
