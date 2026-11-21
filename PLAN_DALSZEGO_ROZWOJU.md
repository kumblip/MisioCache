## Stan po v0.1.3.0

Etap 1 (instalowalna wtyczka, menu, aktualizacje) — wykonany.
Etap 2 (pasywna obserwacja HTTP i natywnych sesji) — kod gotowy do weryfikacji na NAS-ie.
Etap 3 (kliencki cache Direct Play/Direct Stream bez zmiany odtwarzacza) — **niezaimplementowany**;
uwarunkowany wynikami testów i dostępnymi punktami integracji w aplikacji mobilnej.

# Plan MisioCache (oddzielony od MisioFlixa)

## Cel

Bez zmiany odtwarzacza Jellyfin: po rozpoczęciu filmu urządzenie pobiera go, jeśli możliwe, aż do końca; przechowuje też wcześniejsze fragmenty do ukończenia seansu; pauza nie zatrzymuje pobierania; przy cofnięciu/wznowieniu fragmenty pobierane są z cache, a nie ponownie z NAS-a. Retencja po przerwaniu i limit dysku konfigurowalne. W razie braku cache: normalny odtwarzacz działa bez zmian.

## Co zawiera pierwsze wydanie 0.1

- Oddzielna wtyczka z własnym GUID, menu admina (dwa wpisy), zapis preferencji i diagnostyka możliwości magazynu przeglądarki.
- GitHub Actions: build, samodzielny release po każdym zatwierdzonym push na main, czteroczłonowa wersja, paczka i Jellyfin manifest.
- **Brak kodu przejmującego, proxy'ującego lub modyfikującego playback; brak buforowania wideo.**

## Kolejne kamienie milowe

1. Analiza żądań Direct Play/Direct Stream w używanym kliencie Jellyfin i dostępnych punktów rozszerzania. Priorytet: Android/iOS i przeglądarki bez wymiany natywnego playera.
2. Eksperymentalny adapter **tylko na wyraźnie obsługiwanym kliencie**, z mechanizmem pełnego wyłączenia (fail open) i testem zakresowych żądań Range.
3. Poprawne magazynowanie segmentów, brak ponownego pobierania, retencja aż do końca odtwarzania, limity urządzenia, czyszczenie po zakończeniu. Bez samowolnego wymuszania cache w tle w systemach iOS/Android.
4. Pobieranie w przód do końca podczas odtwarzania oraz pauzy, bez równoległych nadmiernych transferów blokujących urządzenie i z obsługą anulowania.
5. Testy różnych kodeków/kontenerów, przewijania, odłączania sieci, rozłączenia Tailscale, wznawiania po ponownym otwarciu oraz niedostatecznej pamięci.

## Dlaczego serwer sam nie wystarczy

Jellyfin uruchamia odtwarzanie w klientach; plugin DLL na NAS-ie nie może sam zapisać danych na telefonie. Rozwiązanie JellyPlay oparte na `ExoPlayer Media3 SimpleCache` dotyczy innej aplikacji i jej własnego ExoPlayera. Nie kopiować go jako odpowiednika integracji z oficjalnym Jellyfin.

## Bezpieczeństwo

Nie kopiowano GPL-3.0 JellyPlay; odtwarzacz, DRM, napisy, audio, MAL/Simkl MisioFlixa pozostają nietknięte. Integracja wymaga testu rzeczywistego klienta, a nie tylko pozytywnych asercji kodu.
