# MisioCache v0.1.4.0 — analiza ponownie pobieranych zakresów

## Co jest nowego

- Pasywna, opcjonalna analiza `Range` i `Content-Range` dla strumieni plikowych `GET /Videos/{guid}/...` przy HTTP 206.
- Anonimowe przypisywanie odpowiedzi do tego samego materiału przez tymczasowy HMAC; sekretny klucz istnieje tylko w RAM i jest zmieniany przy każdym restarcie serwera.
- Obliczenia **zadeklarowanych długości zakresów**: sumy, unii (unikalnych) oraz bajtów w nakładających się zakresach. Licznik odpowiedzi z nakładaniem.
- Ostatnie 25 zakresów z godziną UTC, M1/M2…, numerami początkowego i końcowego bajtu oraz długością i nakładaniem.
- Bez nazw, źródłowych ID, URL-i, zapytań, tokenów, IP, danych użytkowników, treści wideo ani modyfikowania playera.

## Bardzo ważne ograniczenie

`Content-Range: bytes 500-1499/10000` oznacza odpowiedź **deklarującą** zakres 1000 bajtów. Nie dowodzi, że urządzenie odebrało wszystkie bajty: transmisja mogła zostać przerwana. Wartość „nakładające się bajty” jest więc miarą **powtórzenia zakresów żądanych i zadeklarowanych w odpowiedziach**, a nie precyzyjnym pomiarem transferu na przewodzie, dysku urządzenia lub trafień w cache.

Obserwator nie zapisuje danych filmu i nie pobiera go do końca. Jest pasywny. Nie rejestruje zakresów `HEAD`, nie zestawia zakresów wielu plików i nie wyciąga wniosków o buforze innego urządzenia. Dla HLS/DASH osobno zliczane są tylko requesty, bez analizowania payloadu segmentów.

## Instalacja / GitHub

1. Rozpakuj `MisioCache_v0.1.4.0_TYLKO_ZMIANY_OD_v0.1.3.0.zip` i prześlij pliki w tej samej strukturze do istniejącego repozytorium `kumblip/MisioCache`, gałąź `main`.
2. Nie zastępuj `manifest.json`. Jest zarządzany przez istniejący mechanizm Release i katalog Jellyfina.
3. Po zatwierdzeniu wykonaj kontrolę workflow **MisioCache — automatyczny Build i Release**. Test .NET sprawdza **oryginalną implementację klasy pomiarowej**, a nie jej makietę.
4. Po udanym Release zainstaluj aktualizację przez panel MisioCache/Jellyfin, a następnie zrestartuj Jellyfin, żeby aktywować nowy kod middleware.

Wersja źródeł to `0.1.4.0`; rzeczywistą wersję publikacji nadaje workflow na podstawie najnowszych tagów. Jeżeli GitHub opublikował wcześniej inny numer, to wynik Action jest autorytatywny.

## Test cofania na Androidzie (Direct Play)

1. Upewnij się, że pasywna obserwacja jest włączona.
2. **Raz** kliknij „Wyzeruj liczniki” **przed rozpoczęciem próby**.
3. Odtwarzaj wybrany fragment odcinka przez około minutę.
4. Zapisz widoczne zakresy i liczniki w panelu, **nie resetując ich**.
5. Cofnij odcinek o 10–30 sekund do przed chwilą obejrzanego fragmentu.
6. Naciśnij „Sprawdź odtwarzanie i ruch” i porównaj wzrost `Odpowiedzi z nakładającymi się zakresami` oraz `Deklarowane bajty żądane ponownie`.

Jeżeli oba liczniki rosną, serwer odebrał prośbę o obszar bajtowy, który pojawił się wcześniej w rozpoznanej odpowiedzi 206 tego samego materiału. Jeśli nie rosną, może to oznaczać brak powtórzonych zakresów, niepełne nagłówki albo brak zdarzeń w oknie (30 minut/2048 próbek). **Nie interpretuj braku przyrostu jako potwierdzonego lokalnego cache.**

## Zgodność i granice zakresu

- Dwa natywne wpisy w menu, funkcje aktualizacji, osobne repozytorium i automatyczne wydania pozostają.
- MisioFlix v0.4.121.0 i odtwarzacz są nietknięte.
- Przełącznik obserwacji pozostaje wyłączony domyślnie; po wyłączeniu nie zbiera się nowych próbek.
- 30 minut, maksymalnie 2048 zdarzeń w pamięci serwera; zerowanie niszczy kontekst porównania.
- Kompilacja i test integracji z Jellyfin 12.4 wymagają GitHub Actions; lokalnie nie ma .NET SDK.

## Niezmienne GitHub Actions

W tej skorygowanej paczce v0.1.4.0 oba workflow `.github/workflows/build.yml`
i `.github/workflows/release.yml` pozostają dokładnie takie jak w v0.1.3.0.
Nie trzeba ich wgrywać ani edytować. Każdy nowy commit na `main` nadal
uruchamia automatyczny Build / Release z automatyczną wersją.

Nowe testy C# nie potrzebują dodatkowych kroków YAML: workflow już wykonują
`python3 tools/run_tests.py`, a ten skrypt sam odkrywa projekty
`tests/dotnet/**/*.csproj` i uruchamia je przez `dotnet run`.
Brak SDK .NET w GitHub Actions jest błędem blokującym Release; lokalnie,
gdy SDK nie jest dostępne, testy .NET są jawnie pomijane.

**Wgrywanie:** wybierz paczkę TYLKO_ZMIANY_BEZ_WORKFLOW i prześlij zawarte
pliki do repozytorium, nie edytuj plików `.github/workflows/` ani `manifest.json`.
