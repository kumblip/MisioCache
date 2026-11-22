# MisioCache — Smart Downloads i oczyszczenie ustawień

To jest **zastępcza paczka** w stosunku do dwóch poprzednich paczek Smart Downloads. Nie wgrywaj ich osobno.

## Zmiany
- Zachowano opcjonalne Smart Downloads: zgoda oddzielna dla każdego konta i telefonu, domyślnie wyłączone.
- Dowolna **dodatnia całkowita** liczba następnych odcinków (domyślnie 2).
- Dowolny dodatni limit paczki w **GB**, także ułamkowy wpisany z przecinkiem.
- Zachowano oficjalne natywne pobrania Androida i menu „Pobrane”.
- Usunięto z panelu administratora HTTP Range, transfery, przewijanie, przeglądarkowe API cache i nieaktualną politykę cache filmu.
- Usunięto rejestrację obserwatora HTTP w serwisach Jellyfina i funkcje liczące transfer; dawne pliki C# są puste, żeby nadpisanie przez GitHub Web nie wymagało ręcznego kasowania.
- Zachowano i przetestowano panel aktualizacji z automatycznym odświeżaniem katalogu.

## Niezaimplementowane (ograniczenie oficjalnej aplikacji)
- Lokalny odczyt statusu pobrań po identyfikatorze odcinka.
- Automatyczne usuwanie obejrzanych, automatyczne uzupełnianie kolejki bez udziału użytkownika.
- Odtwarzanie pobranej kopii po zwykłym kliknięciu w odcinek.
- Globalny pomiar dostępnego miejsca telefonu. Limit GB dotyczy *wyłącznie szacunkowej nowej paczki*.

## Instalacja
1. Rozpakuj ZIP i wgraj pliki ze środka do obecnego repo `kumblip/MisioCache` na `main`, zachowując podfoldery.
2. Nie wgrywaj samego ZIP-a do repo. Nie nadpisuj `.github`, `meta.json` ani `manifest.json`.
3. Zatwierdź commit; sprawdź istniejący GitHub Actions i Release. Nie ma ręcznej zmiany wersji.
4. Po udanym Release zaktualizuj wtyczkę w Jellyfin i zrestartuj serwer (usuwa aktywny dotychczas middleware z uruchomionej DLL).
5. Smart Downloads jest widoczne w menu `MisioCache`; `MisioCache — ustawienia` zawiera tylko panel aktualizacji oraz informację o funkcji.

## Testy
Uruchomione lokalnie testy Python/Node. Testy C# i pełna kompilacja .NET 10 wymagają istniejącego GitHub Actions.
