# MisioCache — standardowe „⋮ → Pobierz” + następne odcinki

## Do którego repozytorium?

**Wyłącznie `kumblip/MisioCache`**. MisioFlix pozostaje odpowiedzialny tylko za przycisk i popup. Ten ZIP jest paczką źródłową **tylko zmienionych plików**, bez `.github`, `meta.json`, `manifest.json` i numerów wersji. Nie przesyłaj ZIP-a jako pojedynczego pliku do repozytorium: rozpakuj go i prześlij pliki, zachowując ich ścieżki.

## Co robi poprawka

- MisioCache sam wstrzykuje własny JS do **index.html** Jellyfin Web przez ASP.NET `IStartupFilter` i udostępnia go spod `/MisioCache/assets/smart-downloads.js`. Nie modyfikuje zainstalowanego Jellyfin Web, nie zmienia APK i nie korzysta ze skryptów MisioFlixa.
- Przechwytuje **tylko wywołania** standardowych `NativeShell.downloadFile()` i `NativeShell.downloadFiles()` w oficjalnym Jellyfin Android. Najpierw natychmiast przekazuje oryginalne pobranie bez zmian.
- Gdy pobranie dotyczy **jednego odcinka** i użytkownik wcześniej włączył Smart Downloads, odczytuje z Jellyfin API odcinki serialu, sortuje je i próbuje dołożyć N następnych (domyślnie 2).
- Zgoda i ustawienia `misio.cache.smart.v1:<serwer>:<konto>:<telefon>` są współdzielone z popupem MisioFlixa — bez zmian w MisioFlix. Przy innym użytkowniku, telefonie lub wyłączonej opcji żadne dodatkowe pobierania nie są zlecane.
- Limit GB dotyczy oszacowanego rozmiaru **wybranego + kolejnych** w ramach tej akcji. Jeśli rozmiar jest nieznany albo limit przekroczony, pobieranie wybranego działa jak zwykle, a dodatkowe zostają pominięte.
- Ręczne „Pobierz wybrany + kolejne” w panelu MisioCache korzysta z oryginalnego downloadera, bez wtórnego uruchamiania automatu. Dwa razy zlecone dodatkowe odcinki są deduplikowane w ramach sesji widoku WebView.
- Ostatnia przyczyna sukcesu lub odmowy automatycznej kolejki jest dostępna w **MisioCache — pobieranie**.

## Czego ta poprawka NIE robi

- Nie ma API do sprawdzenia, co już zostało pobrane, faktycznego wolnego miejsca telefonu ani ukończenia natywnego transferu. „Zlecono” nie znaczy „pobrano”. Nie zastępuje lokalnego odtwarzania i nie synchronizuje obejrzenia.
- Nie usuwa obejrzanych odcinków ani nie uzupełnia kolejki po zakończeniu oglądania. Uzupełnienie następuje tylko po **nowym standardowym kliknięciu Pobierz** (lub ręcznej paczce w MisioCache).
- Nie uruchamia dodatkowej kolejki po wcześniej zakończonych pobraniach, bez kolejnego kliknięcia.
- Nie przechwytuje ruchu wideo ani nie zmienia odtwarzacza; obsługa oficjalnego polecenia zależy od tego, czy używany klient wywołuje `NativeShell` w Jellyfin Web.

## Wdrożenie

1. Wgraj tylko pliki z tego ZIP do istniejącego repozytorium `kumblip/MisioCache` na `main`. Nie wgrywaj wycofanej paczki `MisioFlix_SMART_DOWNLOADS_AUTOMAT_PO_STANDARDOWYM_POBIERZ_TYLKO_ZMIANY.zip`.
2. `Commit changes` uruchomi automatyczne Actions MisioCache (testy → kompilacja .NET 10 → numer wydania → ZIP DLL → manifest). Nie zmieniaj `.github` ani numerów wersji.
3. Po pomyślnym Release zainstaluj nową wersję MisioCache przez Jellyfin i **zrestartuj serwer**, aby zarejestrować nowy filtr HTML.
4. Otwórz oficjalny Jellyfin Android, w popupie Smart Downloads w MisioFlix zaznacz opcję, zapisz np. **2** kolejne, limit **5 GB**.
5. Wybierz odcinek, który nie jest jeszcze pobrany, np. E02 → **⋮ → Pobierz**. Oczekiwane: standardowy E02 + zlecenie E03 i E04, jeżeli rozmiary i limit pozwalają. Sprawdź natywną sekcję **Pobrane**.
6. Jeśli pobierze się tylko E02, sprawdź panel **MisioCache — pobieranie → Ostatnia próba**. Jeżeli nie ma żadnej wiadomości, zweryfikuj czy żądanie `MisioCache/assets/smart-downloads.js` rzeczywiście załadowało się do WebView. Funkcja nie została jeszcze sprawdzona w prawdziwej aplikacji Android i nie może być uznana za potwierdzoną, dopóki test na telefonie nie przejdzie.

## Wyniki testów

Lokalnie: 32 testy Python zaliczone + 1 pominięty (`.NET SDK` nie ma w środowisku) oraz test Node własnej kolejki. Kompilacja i rzeczywisty Android — **do weryfikacji** w GitHub Actions i na telefonie.
