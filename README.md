# Island Raiders

Unity 6 ve Netcode for GameObjects ile yapılmış, coop çalışabilen bir ada/deniz oyunu. Oyuncular tekneyle denizde dolaşıp adalarda silah topluyor, AI gemilere binip mürettebatla çatışıyor ve ele geçirdikleri sandıkları açarak para kazanıyor.

## Gereksinimler

* Unity 6000.3.15f1
* Netcode for GameObjects (proje zaten bu paketle geliyor)

## Oyunu Başlatma

Ana menüde iki seçenek var:

* **Host**: kullanıcı adını girip yeni bir dünya başlatırsın (veya kayıtlı bir dünyayı seçip host olarak yüklersin). Sen host olduğunda diğer oyuncular senin IP adresine bağlanabilir.
* **Join**: host'un IP adresini girip bağlanırsın.

Oyun tek başına da (sadece host, başka client olmadan) sorunsuz oynanabilir.

Kayıt menüsünden önceki dünyalardan birini seçip devam edebilir ya da kaydı silebilirsin. Oyun sırasında dünya durumu (gemiler, mürettebat, envanter, can, para) kaydedilip sonra kaldığın yerden yüklenebiliyor.

## Kontroller

Hareket W A S D, bakış yönü fare. Sol Shift koşma, boşluk zıplama (yüzerken yüzeye çıkma tuşu da aynı). Sol tık elde silah varken ateş eder. E tuşu genel etkileşim tuşu: yerden eşya/silah/sandık alma, dümene geçip bırakma, elindeki sandığı açma hep bununla oluyor. G elde tuttuğun eşyayı yere bırakır. Hotbar'da slot seçmek için 1-6 tuşları veya fare tekerleği kullanılıyor. Esc fare imlecini kilitleyip serbest bırakıyor.

Suya girince otomatik olarak yüzme moduna geçersin; bakış yönün ileri/geri hareketi, boşluk tuşu yukarı çıkışı belirler. Bir geminin güvertesine ya da adaya yakın kıyıya bakıp boşluğa basarsan sudan tırmanıp çıkarsın.

### Gemi Sürme

Bir dümene bakıp E'ye basınca dümeni ele alırsın; tekrar E ile bırakırsın. Dümendeyken W/S hızı, A/D dönüşü kontrol eder. Dümendeyken ateş edemezsin.

### Envanter ve Sandık

E ile yerden alınan silahlar ve sandıklar hotbar'a eklenir. Bir sandığı hotbar'dan seçip E'ye basarsan sandığı açarsın ve rastgele bir miktar para kazanırsın. Silahların sınırlı mermisi var; mermi bitince aynı silahı yerden tekrar almadan doldurma imkânı yok. Bıraktığın (G) silah, üzerinde kalan mermiyle birlikte yere düşer.

## AI Gemiler ve Mürettebat

Haritada devriye gezen AI gemiler var; her gemide rastgele sayıda mürettebat ve bazen bir ödül sandığı bulunuyor. Mürettebat başta sana saldırmaz, güvertede kendi rotasında dolaşır. Bir gemideyken ateş açarsan ya da o geminin sandığını çalarsan, o geminin mürettebatı seni fark edip peşine düşer ve size doğru gelip çatışmaya girer — bu tepki sadece o gemiye özeldir, yakındaki başka bir gemiye sıçramaz.

## Ekranda Görünenler

Sağ altta can barın ve sağ üstte para miktarın sürekli görünür. Elinde silah varken kalan mermi sayısı da gösterilir. Önemli olaylarda (sandık açma, ölüm, mürettebatın saldırıya geçmesi, mermi bitmesi gibi) ekranın üstünde kısa bir bildirim belirir.

## Test / Geliştirici Paneli

Editor'da veya development build'de oyun içindeyken **F1** ile bir QA paneli açılır (can doldurma, silah/mermi/para verme, gemi/mürettebat spawn etme, kayıt alma gibi kısayollar içerir). Bu panel sadece test amaçlıdır, normal oynanışın bir parçası değildir.

