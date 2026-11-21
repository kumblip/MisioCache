MisioCache — aktualizacja panelu wtyczki (kolejna wersja po 0.1.0.0)

Paczka TYLKO ZMIANY jest przeznaczona dla istniejacego repozytorium kumblip/MisioCache.
Rozpakuj, wgraj jej ZAWARTOSC do repozytorium GitHub, zastap istniejace pliki
z tymi samymi nazwami, a nowe dodaj. Wybierz Commit changes na main.
Nie wgrywaj ponownie pustego manifest.json ani nie podmieniaj meta.json.
Workflow automatycznie wyznaczy numer nastepnego wydania na podstawie
opublikowanych tagow i manifestu (przy v0.1.0.0 -> v0.1.1.0).

Stara wtyczka nie ma jeszcze przycisku wymuszenia; nowa bedzie widoczna
po jednorazowej instalacji wersji z tymi zmianami przez natywny katalog
wtyczek Jellyfin. Pozniejsze wydania mozna odswiezac w panelu.

Najwazniejszy nowy ekran: MisioCache — ustawienia -> Aktualizacje MisioCache.
Wymus aktualizacje katalogu Jellyfina -> (po weryfikacji) Zainstaluj aktualizacje.
Instalacja nigdy nie jest automatyczna. Po aktualizacji moze byc konieczny restart.
