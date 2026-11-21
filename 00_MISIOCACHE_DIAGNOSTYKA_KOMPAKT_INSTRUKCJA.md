# MisioCache — następny automatyczny Release (po v0.1.5.0)

Paczka zawiera **tylko zmienione / nowe pliki źródłowe**. Nie zawiera `.github`, `meta.json`, `manifest.json`, binarnych DLL ani tagów; niczego tam nie trzeba ręcznie edytować. Wgrywaj zawartość ZIP-a, a nie ZIP jako jeden plik, do `kumblip/MisioCache` na gałąź `main`, zachowując ścieżki. GitHub Actions automatycznie wybierze wersję (przy ostatnim v0.1.5.0 przewidujemy v0.1.6.0), wykona testy, .NET build i Release.

## Nowe pomiary w MisioCache

- Pulpit na pierwszym ekranie, cztery grupy: żądania, zapis do HTTP, zmiany początku Range przód/tył i jakość odpowiedzi; ostatnich sześć zakresów od razu, 25 i stare liczniki w zwijanych szczegółach.
- Powtórzone bajty liczone nie tylko z nagłówka `Content-Range`, ale też ze znanych fragmentów **przekazanych do zapisu odpowiedzi HTTP**. Dla odpowiedzi 206 z pojedynczym, poprawnym Range odpowiadają one spójnemu prefiksowi początkowego zakresu. Osobne sumy nowych, unikalnych i powtórzonych bajtów; kierunek ruchu i wielkość przesunięcia startu zakresu.
- Czas odpowiedzi, czas pierwszego zapisu, orientacyjna szybkość przekazywania do transportu, przerwane lub niepełne odpowiedzi, odpowiedzi bez danych pomiarowych.
- Osobny, **DOMYŚLNIE WYŁĄCZONY** przełącznik "Pomiar bajtów"; podstawowe pasywne liczniki pozostają niezależne. Pomiar owija metody zapisu `IHttpResponseBodyFeature` (Stream, PipeWriter, SendFile) bez czytania, przechowywania, buforowania ani modyfikowania danych. Zwiększa nieco narzut podczas testu, dlatego po pomiarach go wyłącz.
- Nie modyfikuje MisioFlixa, odtwarzacza Jellyfin, sesji, API wtyczki ani procesu wydawniczego.

## Ściśle rozdzielone znaczenia pomiarów

- **Content-Range** = zakres zadeklarowany przez serwer, nie transfer.
- **Bajty przekazane do zapisu odpowiedzi HTTP** = liczba bajtów przyjętych przez interfejs zapisu w procesie serwera. Nie dowodzi odebrania przez Androida, wykorzystania bufora telefonu ani fizycznego transferu po kablu. Przy anulowaniu PipeWriter może zawierać nieprzesłane jeszcze bajty.
- **Brak pomiaru** ≠ 0 B. Jeśli metoda zapisu nie jest mierzalna, odpowiedź ma wynik nieznany. Uwaga: w razie nietypowego streamingu należy zweryfikować zachowanie w testach Jellyfin.
- **Przód/tył** = rosnący/malejący początek następnego Range tego samego materiału (anonimowe HMAC); nie jest to odczyt czasu odtwarzania, więc przy odczytach sekwencyjnych wzrost nie zawsze znaczy ręczne przewinięcie.
- Liczniki resetują się na żądanie, po restarcie i po 30 minutach; maksimum 2048 wpisów, tylko RAM.

## Krótki test Android DirectPlay

1. Zainstaluj nowszy Release przez Jellyfin i uruchom ponownie Jellyfin.
2. W MisioCache → ustawienia zaznacz **Obserwacja** i **Pomiar bajtów**. Wyzeruj liczniki **tylko raz na początku**.
3. Odtwarzaj 30–60 sekund, kliknij Odśwież (zdjęcie #1).
4. Przewiń DO PRZODU o 20–60 sekund, odtwarzaj 20–30 sekund, Odśwież (#2).
5. Cofnij o 20–60 sekund do obejrzanego materiału, odtwarzaj, Odśwież (#3). Bez resetu między punktami.
6. Prześlij trzy zrzuty pulpitu + w razie nieznanego transferu rozwiniętą tabelę 25 zakresów. Wyłącz Pomiar bajtów po teście.

## Weryfikacja

42 testy Pythona i Node przeszły lokalnie (1 test pominięty celowo). Brak lokalnego .NET SDK, dlatego testy C# (silnik zakresów + produkcyjny dekorator zapisu) i **kompilację pluginu .NET 10/Jellyfin 12.4 musi potwierdzić GitHub Actions**. Dopóki nie ma zielonego wyniku, wersja jest paczką źródeł do walidacji, nie zweryfikowaną DLL.
