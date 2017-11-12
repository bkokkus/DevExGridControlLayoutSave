using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows.Forms;
using DevExpress.XtraGrid;

namespace FirmaDanismanlik.cs
{
    class csGridLayout : IDisposable
    {
        internal enum enGridLayoutIslemleri { get, set }
        // Control nesnesi, form nesnesinin "using System.Windows.Forms;" class ı içerisindedir
        private Control _gelenForm;
        private int _personelID;
        private SqlConnection _baglanti;
        private SqlTransaction trGenel;
        private enGridLayoutIslemleri _islem;

        public csGridLayout(enGridLayoutIslemleri islem, Control gelenForm, int personelID,
          SqlConnection baglanti)
        {
            //dışarıdan alınan değişkenler class içinde kullanılmak için private değişkenlerde 
            //saklanıyor.
            _islem = islem;
            _gelenForm = gelenForm;
            _personelID = personelID;
            _baglanti = baglanti;

            trGenel = baglanti.BeginTransaction();
            FormdakiGridleriBul(_gelenForm, _islem);
            trGenel.Commit();
        }

        private void FormdakiGridleriBul(Control nesne, enGridLayoutIslemleri islem)
        {
            if (nesne is DevExpress.XtraGrid.GridControl)
            {
                if (islem == enGridLayoutIslemleri.set)
                    GridArayuzKaydet(nesne);
                else
                    GridArayuzYukle(nesne);
            }
            foreach (Control altNesne in nesne.Controls)
            {
                FormdakiGridleriBul(altNesne, _islem);
            }
        }

        void GridArayuzKaydet(Control ctrl)
        {
            var gc = new GridControl(); // using e "DevExpress.XtraGrid" eklerseniz GridControl nesnesini direk tanıyacaktır.
            gc = ctrl as GridControl;
            var gv = (DevExpress.XtraGrid.Views.Grid.GridView)gc.Views[0]; //box lama

            String layout = "";
            using (var ms = new MemoryStream()) //MemoryStream i tanıması için "System.IO" sınıfını using e eklemeliyiz 
            {
                //ASrayüzdeki grid üstünde veriyi. dışarı çıkarmak için ms kullanmak zorundayız.
                gv.SaveLayoutToStream(ms);
                ms.Position = 0;
                using (var reader = new StreamReader(ms))
                {
                    layout = reader.ReadToEnd();
                }
            }
            InsertLayout(_personelID, _gelenForm.Name, gv.Name, layout, _baglanti, trGenel);
        }

        void GridArayuzYukle(Control ctrl)
        {
            //veritabanından okunan gridLayout değişkeni önce RAM e sonrada burdan 
            // grid in arayüz değişkenine yazılıyor.
            var gc = new GridControl();
            gc = ctrl as DevExpress.XtraGrid.GridControl;
            var gv = (DevExpress.XtraGrid.Views.Grid.GridView)gc.Views[0];

            MemoryStream ms = getLayout(_personelID, _gelenForm.Name, gv.Name, _baglanti, trGenel);
            if (ms.Length > 0)
                gv.RestoreLayoutFromStream(ms);
        }

        //Veritabanından Grid arayüzü alınıp RAM de "ms" değişekninde saklanıyor.
        private static MemoryStream getLayout(int personelID, string formName, string gridName, SqlConnection baglanti, SqlTransaction trGenel)
        {
            var ms = new MemoryStream();
            using (SqlCommand cmd = new SqlCommand(@"Select GridLayout From GridLayout
Where PersonelID=@PersonelID AND FormName=@FormName AND GridName=@GridName", baglanti, trGenel))
            {
                cmd.Parameters.Add(@"PersonelID", SqlDbType.Int).Value = personelID;
                cmd.Parameters.Add(@"FormName", SqlDbType.VarChar).Value = formName;
                cmd.Parameters.Add(@"GridName", SqlDbType.VarChar).Value = gridName;
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        //Veritabanından okunan değer RAM e yazılıyor.
                        string data = dr["GridLayout"].ToString();
                        byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);
                        ms.Write(buffer, 0, buffer.Length);
                        ms.Seek(0, SeekOrigin.Begin);
                    }
                }
                return ms;
            }
        }

        public static void InsertLayout(int personelID, string formName, string gridName, string gridLayout, SqlConnection baglanti, SqlTransaction trGenel)
        {
            string gridLayoutID = "-1";
            //"daha önce bu gird e ait bir veri kaydedilmiş mi?" diye sorduk.
            using (SqlCommand cmd = new SqlCommand(@"Select GridLayoutID From GridLayout
Where PersonelID=@PersonelID AND FormName=@FormName AND GridName=@GridName",
       baglanti, trGenel))
            {
                cmd.Parameters.Add("@PersonelID", SqlDbType.Int).Value = personelID;
                cmd.Parameters.Add("@FormName", SqlDbType.VarChar).Value = formName;
                cmd.Parameters.Add("@GridName", SqlDbType.VarChar).Value = gridName;

                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                        gridLayoutID = dr["GridLayoutID"].ToString();
                }
            }
            //Veritabanında parametrelere uygun satır bulunamadıysa, insert; bulunduysa aynı satır için
            //update işlemi çalıştırdık.
            if (gridLayoutID != "-1")
            {
                //satır güncelleniyor
                using (SqlCommand cmd = new SqlCommand(@"Update GridLayout SET GridLayout=@GridLayout 
Where PersonelID=@PersonelID AND FormName=@FormName AND GridName=@GridName",
                  baglanti, trGenel))
                {
                    cmd.Parameters.Add("@PersonelID", SqlDbType.Int).Value = personelID;
                    cmd.Parameters.Add("@FormName", SqlDbType.VarChar).Value = formName;
                    cmd.Parameters.Add("@GridName", SqlDbType.VarChar).Value = gridName;
                    cmd.Parameters.Add("@GridLayout", SqlDbType.NVarChar).Value = gridLayout;

                    cmd.ExecuteNonQuery();
                }
            }
            else
            {
                //yeni satır ekleniyor.
                using (SqlCommand cmd = new SqlCommand(@"Insert Into GridLayout(PersonelID,FormName,GridName,GridLayout) 
values(@PersonelID,@FormName,@GridName,@GridLayout)",
                  baglanti, trGenel))
                {
                    cmd.Parameters.Add("@PersonelID", SqlDbType.Int).Value = personelID;
                    cmd.Parameters.Add("@FormName", SqlDbType.VarChar).Value = formName;
                    cmd.Parameters.Add("@GridName", SqlDbType.VarChar).Value = gridName;
                    cmd.Parameters.Add("@GridLayout", SqlDbType.NVarChar).Value = gridLayout;

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

       
    }
}
