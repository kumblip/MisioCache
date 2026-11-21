# MisioCache v0.1.3.0 — instrukcja wdrożenia i testowania

## Paczki

- `MisioCache_v0.1.3.0_TYLKO_ZMIANY_OD_v0.1.2.0.zip`: minimalne podmiany, **bez** manifest.json.
- `MisioCache_v0.1.3.0_GITHUB_PELNE_BEZ_MANIFESTU.zip`: wszystkie źródła do istniejącego repo, także **bez** manifest.json, żeby zachować historię wydań.

## Przed aktualizacją

1. Sprawdź na GitHubie najnowszy tag w Releases. Wersja 0.1.3.0 jest celem przygotowanym
   po planowanym 0.1.2.0; nie mogliśmy odczytać aktualnego tagu z repo GitHub.
2. Utwórz kopię obecnych źródeł i `manifest.json`. Nie wgrywaj ZIP-a jako jednego pliku.
3. Wypakuj i wgraj *pliki ze środka ZIP-a* z zachowaniem ścieżek. Przy małym limicie GitHub
   lepszy będzie GitHub Desktop: Clone, Copy, Commit to main, Push origin.
4. Nie usuwaj aktualnego `manifest.json` ani istniejących tagów; automatyczny Release
   zweryfikuje wersję i w razie konfliktu wyznaczy kolejną.
5. Otwórz Actions -> MisioCache automatyczny Build i Release. Dopiero **zielony Build**
   i nowy Release potwierdzają, że kod rzeczywiście się kompiluje.

## Instalacja i pomiar

1. Zainstaluj Release przez natywny katalog Jellyfina (można wymusić aktualizację w panelu).
2. Zrestartuj serwer po zainstalowaniu nowej DLL — wymagane dla obserwatora IStartupFilter.
3. W panelu MisioCache sprawdź `Stan obserwatora`: powinno pokazać `Wyłączona`
   przy `pipelineInstalled=true`. Jeśli brak aktywnego obserwatora, sprawdź log i restart.
4. Włącz „pasywną obserwację HTTP wideo”. Jest to świadome włączenie dla administratora.
5. Otwórz film na urządzeniu w istniejącym odtwarzaczu i odczekaj na ładowanie kolejnych
   fragmentów. Kliknij „Sprawdź odtwarzanie i ruch”.
6. Sprawdź aktywną sesję i PlayMethod. Obserwuj liczbę Range i 206 podczas odtwarzania;
   na pauzie i przy cofaniu sprawdź, czy pojawiają się kolejne żądania.
7. „Wyzeruj liczniki” usuwa próbki z RAM, nie wpływa na seans i niczego nie usuwa z dysku.

## Ograniczenia

- HTTP 206 oznacza odpowiedź częściową z NAS-a, a nie potwierdzenie lokalnego cache.
- Nie ma pomiaru bajtów, obciążenia sieci ani faktycznego czasu buforowania na telefonie.
- Sesje pokazują tylko PlayMethod zgłoszony przez danego klienta; w przeciwnym razie „Nieznany”.
- Nie jest to odtwarzanie offline, buforowanie do końca ani zapis fragmentów filmu.
- Włączony obserwator wprowadza niewielkie, dodatkowe przetwarzanie każdego żądania wideo;
  w razie problemów natychmiast wyłącz go w panelu.
- Na serwerze Jellyfin 12.4 wymagana jest kompilacja .NET 10 w Actions — lokalnie nie
  była dostępna. Błędy kompilacji trzeba ocenić według konkretnego logu Actions.

## Bezpieczeństwo i brak regresji

- Zero zmian do MisioFlix v0.4.121.0.
- Nie zmieniono istniejących endpointów strumieniowania ani kontrolera odtwarzania.
- Przechowywane są wyłącznie zanonimizowane liczniki w RAM przez 30 minut / max 2048.
- API statystyk i zerowania dostępne jedynie administratorowi Jellyfina.
- Jeśli serwer nie uruchomi filtra, całe odtwarzanie i dotychczasowe aktualizacje działają bez niego.

## Następny etap

Po uzyskaniu pomiarów z rzeczywistego urządzenia zaprojektować dobrowolny adapter
kliencki dla powtórnego wykorzystywania bajtów. Nie zamieniać odtwarzacza i nie
wprowadzać globalnego proxy ani nadpisywania systemowych plików Jellyfin Web.
