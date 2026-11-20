# Noctyra

Vampir – Köylü (Mafya / Werewolf tarzı) bir sosyal çıkarım oyunu. Unity 6 (6000.4.7f1) + URP.
Şu an tek oyunculu: sen + yapay zekâ botları.

## Oyun
- Roller: Köylü, Vampir, Doktor, Falcı, Avcı
- Gece: vampirler avlanır, doktor korur, falcı bakar
- Gün: tartışma (suçla / savun / rol açıkla ya da blöf yap) ve oylama
- 6–12 oyuncu, istediğin rolü seçebilirsin, botlar rol açıklar, blöf yapar, seni suçlar

Arayüz çalışma anında kodla kurulur (`Assets/Scripts/Bootstrap.cs`), sahnede elle bir şey yapmana gerek yok.
Oyun mantığı `Assets/Scripts/Core/GameEngine.cs` içinde, Unity'den bağımsız saf C#.

## Kurulum (oyuncular için)
Releases sayfasından **NoctyraSetup.exe** indir ve çalıştır. Setup son sürümü GitHub'dan indirir,
kurar, kısayol açar; sonraki açılışlarda yeni sürüm varsa "Güncelle" der. Kaldırmak için Setup'taki "Kaldır"
ya da Windows > Uygulamalar.

## Sürüm yayınlama (sen için)

### A) Otomatik (GitHub Actions)
1. Repo > Settings > Secrets > Actions: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
   (rehber: https://game.ci/docs/github/activation)
2. `git tag v0.2.0 && git push origin v0.2.0`
3. Workflow oyunu derler, Setup'ı derler (repo adını exe'ye gömer) ve Release'e `Noctyra-win64.zip` + `NoctyraSetup.exe` ekler.

### B) Kendi bilgisayarından (lisans derdi yok)
`gh auth login` yaptıktan sonra:
```
powershell -ExecutionPolicy Bypass -File Tools\release.ps1 -Version v0.2.0
```
Elle: Unity'de **Noctyra > Build > Windows + Zip** → `Builds/Noctyra-win64.zip`; Setup için
`dotnet build Launcher/NoctyraSetup.csproj -c Release -p:RepoSlug=kullanici/Noctyra`.

Setup, release'teki `.zip` dosyasını bulur; zip'in kökünde `Noctyra.exe` olmalıdır. Repo public olmalı.

## Klasörler
- `Assets/Scripts/Core` oyun kuralları + bot yapay zekâsı
- `Assets/Scripts/UI` menüler, oyun ekranı, gece/gündüz arka planı
- `Launcher` Setup/güncelleyici (.NET Framework 4.8, WinForms)
- `.github/workflows/release.yml` otomatik derleme ve release
