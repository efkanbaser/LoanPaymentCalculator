public string ItfaPlanHesapla(DateTime? krediKullandirmaTarih, decimal? krediTutar, int? vade, int? anaParaOdemesizSure, int? anaParaOdemePeriyot,
    string vadePeriyot, string krediFaizVade, int? krediFaizFaizOran, int? KKDF, int? BSMV, int? faizOdemesizSure)
        {
            // Bu işlemlerin hepsinde anaParaOdemePeriyot aynı zamanda faizin ödeme periyodu olarak kabul edilmiştir

            #region hatalar
            if (krediFaizFaizOran == null)
            {
                return "Kredi faiz oranını giriniz.";
            }
            else if (krediKullandirmaTarih == null)
            {
                return "Kredi kullandırma tarihini giriniz.";
            }
            else if (anaParaOdemesizSure == null)
            {
                return "Ödemesiz süreye bir değer giriniz.";
            }
            else if ((vade - anaParaOdemesizSure) % anaParaOdemePeriyot != 0)
            {
                return "Ödeme periyodunu kontrol ediniz.";
            }
            else if (krediFaizVade != KrediFaizVadeEnum.Yillik.ToDescription()
                && krediFaizVade != KrediFaizVadeEnum.AltiAylik.ToDescription()
                && krediFaizVade != KrediFaizVadeEnum.UcAylik.ToDescription()
                && krediFaizVade != KrediFaizVadeEnum.Aylik.ToDescription())
            {
                return "Faiz vadesini aylık, 3 aylık, 6 aylık veya yıllık olarak seçiniz.";
            }
            else if (vadePeriyot != VadePeriyotEnum.Gun.ToDescription()
                && vadePeriyot != VadePeriyotEnum.Ay.ToDescription()
                && vadePeriyot != VadePeriyotEnum.Yil.ToDescription())
            {
                return "Vade periyodunu seçiniz.";
            }
            else if (faizOdemesizSure > anaParaOdemesizSure)
            {
                return "Faiz ödemesiz süresi ana para ödemesiz süresinden küçük olmalı.";
            }
            else if(anaParaOdemesizSure >= vade)
            {
                return "Ana paranın ödemesiz süresi vadeden büyük veya eşit olamaz";
            }
            #endregion
            #region ödeme ve faiz periyotlarına göre efektif faiz hesabı
            decimal vergisizFaizOrani = 0.00m;
            if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription()) // Yıllık periyoda göre faiz oranı hesabı
            {
                if (krediFaizVade == KrediFaizVadeEnum.Yillik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value);
                else if (krediFaizVade == KrediFaizVadeEnum.AltiAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value * 2 / 100m);
                else if (krediFaizVade == KrediFaizVadeEnum.UcAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value * 4 / 100m);
                else if (krediFaizVade == KrediFaizVadeEnum.Aylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value * 12 / 100m);
            }
            else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription()) // Aylık periyoda göre faiz oranı hesaplama
            {
                if (krediFaizVade == KrediFaizVadeEnum.Yillik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 1200m);
                else if (krediFaizVade == KrediFaizVadeEnum.AltiAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 600m);
                else if (krediFaizVade == KrediFaizVadeEnum.UcAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 300m);
                else if (krediFaizVade == KrediFaizVadeEnum.Aylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 100m);
            }
            else if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription()) // Günlük periyoda göre faiz oranı hesaplama
            {
                if (krediFaizVade == KrediFaizVadeEnum.Yillik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 36000m);
                else if (krediFaizVade == KrediFaizVadeEnum.AltiAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 18000m);
                else if (krediFaizVade == KrediFaizVadeEnum.UcAylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 9000m);
                else if (krediFaizVade == KrediFaizVadeEnum.Aylik.ToDescription())
                    vergisizFaizOrani = (krediFaizFaizOran.Value / 3000m);
            }
            #endregion

            // Vergili faiz oranı hesabı
            decimal kkdfOrani = KKDF.Value / 100m;
            decimal bsmvOrani = BSMV.Value / 100m;
            var vergiliFaizOrani = vergisizFaizOrani + (vergisizFaizOrani * kkdfOrani) + (vergisizFaizOrani * bsmvOrani);

            // Vergili ve vergisiz faiz oranının aylık verileri hesaplanmıştır, ödeme periyodu 1'den büyükse bu faizler ödeme periyoduna göre efektif hallerine çevrilir
            if (anaParaOdemePeriyot > 1)
            {
                vergisizFaizOrani = (decimal)Math.Pow(Decimal.ToDouble((1 + vergisizFaizOrani)), Convert.ToDouble(anaParaOdemePeriyot)) - 1;
                vergiliFaizOrani = (decimal)Math.Pow(Decimal.ToDouble((1 + vergiliFaizOrani)), Convert.ToDouble(anaParaOdemePeriyot)) - 1;
            }

            var kalanBorc = krediTutar.Value;
            var odemePlani = new List<OdemeSatiri>();
            var faizOdenmeyenSuredeBinenFaiz = 0.00m;

            // Ödemesiz sürede binecek faizi hesaplar
            var sadeceFaizOdenenSure = (anaParaOdemesizSure - faizOdemesizSure) / anaParaOdemePeriyot;

            for (int i = 1; i <= faizOdemesizSure; i++)
            {
                kalanBorc += kalanBorc * vergiliFaizOrani;
                faizOdenmeyenSuredeBinenFaiz = kalanBorc - krediTutar.Value;
            }

            for (int i = 1; i <= sadeceFaizOdenenSure; i++)
            {
                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);

                if (i == 1 && faizOdenmeyenSuredeBinenFaiz > 0)
                    kalanBorc -= faizOdenmeyenSuredeBinenFaiz;

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = krediKullandirmaTarih.Value.ToString("dd.MM.yyyy"),
                    TaksitNo = (i).ToString(),
                    TaksitMiktari = i == 1 && faizOdenmeyenSuredeBinenFaiz > 0 ? faizOdenmeyenSuredeBinenFaiz : kalanBorc * vergisizFaizOrani,
                    Anapara = 0,
                    Faiz = i == 1 && faizOdenmeyenSuredeBinenFaiz > 0 ? faizOdenmeyenSuredeBinenFaiz : kalanBorc * vergisizFaizOrani,
                    KalanPara = kalanBorc
                });
            }
            

            int odemePeriyotSayisi = (vade.Value - anaParaOdemesizSure.Value) / anaParaOdemePeriyot.Value;
            // Anüite hesabında kullanılacak çarpan hesabı
            double factor = Math.Pow(Decimal.ToDouble((1 + vergiliFaizOrani)), Convert.ToDouble(odemePeriyotSayisi));
            // Anüite hesabı
            var taksit = Decimal.ToDouble(kalanBorc) * Decimal.ToDouble(vergiliFaizOrani) * factor / (factor - 1);


            for (int i = 1; i <= odemePeriyotSayisi; i++)
            {
                decimal tempTaksit = (decimal)taksit;
                var vergiliFaiz = kalanBorc * vergiliFaizOrani;
                var vergisizFaiz = kalanBorc * vergisizFaizOrani;

                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);

                kalanBorc += vergiliFaiz;

                if (i == odemePeriyotSayisi)
                    tempTaksit = kalanBorc; // son ödeme olduğu için taksitteki hesap hatalarını düzeltmek için kalan borca eşitlenir ama pek işe yaramıyor sanki

                kalanBorc -= tempTaksit;

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = krediKullandirmaTarih.Value.ToString("dd.MM.yyyy"),
                    TaksitNo = (i + sadeceFaizOdenenSure.Value).ToString(),
                    TaksitMiktari = i == 1 && faizOdenmeyenSuredeBinenFaiz > 0 ? faizOdenmeyenSuredeBinenFaiz + tempTaksit : tempTaksit,
                    Anapara = tempTaksit - vergiliFaiz,
                    Faiz = i == 1 && faizOdenmeyenSuredeBinenFaiz > 0 ? faizOdenmeyenSuredeBinenFaiz + vergisizFaiz : vergisizFaiz,
                    KalanPara = kalanBorc < 0.01m ? 0 : kalanBorc
                });
            }

            var sb = new StringBuilder();
            var cultureInfo = new CultureInfo("tr-TR");
            foreach (var satir in odemePlani)
            {
                sb.AppendLine(string.Join("\t",
                    satir.Tarih, satir.TaksitNo,
                    satir.TaksitMiktari.ToString("N2", cultureInfo),
                    satir.Anapara.ToString("N2", cultureInfo),
                    satir.Faiz.ToString("N2", cultureInfo),
                    satir.KalanPara.ToString("N2", cultureInfo)
                ));
            }
            return sb.ToString();
        }
