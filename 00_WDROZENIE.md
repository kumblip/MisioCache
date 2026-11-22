# MISIOCACHE — WSPÓLNE USTAWIENIA / URZĄDZENIA / SMART DOWNLOADS

## Co jest w poprawce
- Dwie pozycje menu: **MisioCache — ustawienia** i **MisioCache** (przekierowanie do oryginalnych szczegółów i wersji wtyczki Jellyfin).
- Pierwsza strona łączy w rozwijanych sekcjach: ustawienia Smart Downloads, rejestr urządzeń, pobieranie i aktualizacje.
- Rejestr per konto/per device token Jellyfin jest zapisywany na NAS-ie w folderze danych Jellyfin / MisioCache, nie w przeglądarce.
- Autoryzowane API: aktualny użytkownik/urządzenie nie mogą edytować cudzego; lista wszystkich urządzeń i edycja są tylko dla administratora.
- Nowy moduł kliencki korzysta z serwerowego stanu MisioCache, migruje jednorazowo dotychczasową zgodę na danym telefonie, zgłasza aktywność oraz ostatnią próbę.
- Standardowe „⋮ → Pobierz” zachowuje oryginalne pobranie, następne odcinki zleca natywnemu Androidowi tylko przy potwierdzonej zgodzie i wystarczającym limicie.
- Nie przechwytuje strumienia wideo, nie modyfikuje APK, MisioFlixa, .github, meta.json ani manifestu.

## Instalacja
1. Wgraj zawartość ZIP do repozytorium `kumblip/MisioCache`, zachowując ścieżki. **Nie wgrywaj całego folderu nadrzędnego**.
2. Commit do `main` uruchamia automatyczne wydanie MisioCache.
3. Sprawdź powodzenie kompilacji i testów .NET w GitHub Actions, zaktualizuj wtyczkę w Jellyfin oraz **zrestartuj Jellyfin** (potrzebny do rejestracji skryptu HTML).
4. Po wdrożeniu obu paczek otwórz oficjalną aplikację Android, popup na głównej stronie MisioFlixa, potwierdź ustawienia. W panelu MisioCache — ustawienia > Urządzenia powinien pojawić się telefon.
5. Pobierz nowy odcinek przez standardowe `⋮ → Pobierz`, a następnie sprawdź listę „Pobrane” i „Ostatnia próba” na liście urządzeń. Ponowne pobranie wcześniej usuniętego pliku nie jest wstecznym triggerem — musi nastąpić nowe kliknięcie po restarcie.

## Znane ograniczenia
Oficjalne API NativeShell nie potwierdza pobrania, więc zapis „Zlecono” oznacza tylko przekazanie prośby do Androida. Wykrywanie obejrzenia i automatyczne usuwanie pobranych plików, uzupełnianie kolejki po obejrzeniu oraz offline-first ze zwykłej biblioteki NIE są zaimplementowane. Lista urządzeń pokazuje tylko te, które po aktualizacji skontaktowały się z MisioCache; „moduł aktywny” nie dowodzi przechwycenia konkretnego polecenia „Pobierz”. Jeśli Android wywołuje inny mechanizm, konieczne będzie dalsze sprawdzenie po komunikacie diagnostycznym.

## Testy lokalne
`python tools/run_tests.py`: 36 testów Python, 35 zaliczonych, 1 pominięty (brak manifest.json w kopii źródeł). Wbudowane testy Node wykonywane z Pythona zaliczone. Brak lokalnego .NET SDK — CI musi jeszcze potwierdzić kompilację i test .NET 10.
