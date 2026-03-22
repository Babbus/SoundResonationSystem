# Tez Özeti

## Unity DOTS Kullanarak Gerçek Zamanlı Sempatik Rezonans Simülasyonu

Fiziksel nesneler, malzeme özellikleri ve geometrileri tarafından belirlenen doğal frekanslarda titreşim yapar, buna cisimlerin öz titreşim frekansı denir. Titreşim yapan bir nesne, yakınındaki başka bir nesneyi onun doğal frekansında veya katlarında titreşirken, enerji ortamda aktarılır — bu olgu sempatik rezonans olarak bilinir. Akustik, yapı mühendisliği ve müzik enstrümanı tasarımındaki önemine rağmen, sempatik rezonansın gerçek zamanlı simülasyonu interaktif uygulamalarda ve oyun motorlarında büyük ölçüde bulunmamaktadır. Bu projeye yönelmemin sebebi bu eksikliktir.

Bu tez, Unity 6'da Veri Odaklı Teknoloji Yığını (DOTS) kullanılarak inşa edilmiş fizik tabanlı bir rezonans simülasyon sistemi sunmaktadır. Sistem, Kirchhoff plaka teorisini kullanarak gerçek malzeme sabitlerinden (Young modülü, yoğunluk, kayıp faktörü, Poisson oranı) ve nesne boyutlarından doğal frekansları hesaplar. Nesneler dikdörtgen kutular olarak modellenir ve yüzey boyutları temel titreşim frekansını belirler — farklı boyutlar farklı frekanslar üretir, farklı malzemeler farklı sönüm karakteristikleri oluşturur; tümü tek bir analitik formülden elde edilir.

Çalışma zamanında sistem, her kaynak-alıcı çifti arasında frekans eşleştirmesi yapar. Eşleştirme, sönümlü harmonik osilatörün diferansiyel denkleminden türetilen Lorentz (Cauchy) frekans yanıt fonksiyonuna dayanır. Ses gürlüğü, kaynakla alıcı arasındaki mesafenin karesiyle ters orantılıdır. Genlik evrimi iki aşamada modellenir: kaynak etkinken genlik kararlı duruma üstel olarak yaklaşır, kaynak kesildiğinde ise üstel olarak söner. Her iki davranışı da kalite faktörü Q = 1/η belirler.

Sistem, klasik diyapazon deneyi ile doğrulanmıştır: iki özdeş çelik diyapazon yakın yerleştirilir, biri vurulur ve ikincisi sempatik olarak titreşmeye başlar — frekans eşleşmeli rezonans yoluyla enerji transferini göstermektedir. Diyapazonların frekansları farklılaştığında (örneğin birine kütle eklenerek), etki kaybolur ve bu, Lorentz yanıtının frekans seçiciliğini doğrular. Simülasyon bu davranışı yeniden üretir: eşleşmiş diyapazonlar alıcıda genlik artışı sergiler; uyumsuz diyapazonlar ihmal edilebilir yanıt gösterir.

Daha geniş bir çerçevede, rezonans davranışı tasarımcı parametreleri yerine fiziksel özelliklerden ortaya çıkar: bir çelik çubuk dakikalarca yüksek frekansta çınlar (Q ~ 10.000), bir ahşap panel daha düşük bir perdede kısa sürede gümbürder (Q ~ 100) ve bir kauçuk blok neredeyse hiç yanıt vermez (Q ~ 10) — tümü farklı malzeme girdileriyle aynı denklemlerden hesaplanır.

<!-- TODO: Faz 3-6 uygulamasından sonra sonuçlar bölümü eklenecek -->
<!-- TODO: Performans kıyaslamaları eklenecek (varlık sayısı ölçeklenmesi, Burst vs yönetilen) -->
<!-- TODO: Algısal değerlendirme yöntemi ve sonuçları eklenecek -->
<!-- TODO: Uygulanırsa FMOD entegrasyon detayları eklenecek -->
