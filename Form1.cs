using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.Structure;
using System.Timers;


namespace ProjektWizyjneV1
{
    public partial class Form1 : Form
    {
        private Size desired_image_size;
        Image<Bgr, byte> imagePodglad; // <> szablon obiektu, <kolor, głębia koloru(na ilu bitach jest zapisany)>
        Image<Bgr, byte> imageRobot;
        Image<Bgr, byte> imageBufforProgowanie;
        Image<Bgr, byte> imageWykrytyGest;
        Image<Bgr, byte> imageAnaliza;
        Image<Bgr, byte> imageBufforAnaliza;
        Image<Bgr, byte> imageWykres;

        VideoCapture camera;

        Point Pc;
        int pozycjaChwytakX;
        int pozycjaChwytakY;
        bool chwytakZamkniety = false;

        private double[] tabelaPromieni, tabelaWartosciSrednich;
        private int liczba_promieni = 720;
        private int opoznienie_rysowania, kat_poczatkowy; //i tak w sumie bym ustawił 0

        bool[] ktory_gest_wg_powierzchni = new bool[5]; // tablica do punktacji
        bool[] ktory_gest_wg_liczby_wierzcholkow = new bool[5]; // tablica do punktacji
        bool[] ktory_gest_wg_stosunku_szerokosc = new bool[5]; // tablica do punktacji
        bool[] ktory_gest_wg_stosunku_dlugosc = new bool[5]; // tablica do punktacji

        private enum operacja
        {
            NOSIGNAL,
            PROGOWANIE,
            WYZNACZSRODEK,
            SYGNATURARADIALNA
        }

        operacja wybranaOperacja = operacja.NOSIGNAL;

        private enum TrybRysowania { NAD_KRZYWA, TYLKO_DANE, TYLKO_KRZYWA }

        public Form1() //wykonuje sie raz
        {
            InitializeComponent();

            desired_image_size = new Size(400, 300); //320,240

            imagePodglad = new Image<Bgr, byte>(desired_image_size); //Utwórz pusty obraz o określonym rozmiarze (w nawiasie ustawienie wielkości obrazu w pixelach)

            imageRobot = new Image<Bgr, byte>(desired_image_size);

            imageBufforProgowanie = new Image<Bgr, byte>(desired_image_size);

            imageWykrytyGest = new Image<Bgr, byte>(400/2,300/2);

            imageAnaliza = new Image<Bgr, byte>(desired_image_size);

            imageBufforAnaliza = new Image<Bgr, byte>(desired_image_size);

            imageWykres = new Image<Bgr, byte>(400, 100);

            try
            {
                //Wewnątrz bloku try piszemy kod co, do którego nie jesteśmy pewni ,że zadziała dobrze za każdym możliwym razem.

                camera = new VideoCapture(1); //w nawiasie ustawienie źródła  pobieranego obrazu (0 = pierwsza podpięta kamera)
                camera.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, imagePodglad.Width); //ustawienie właściwości obrazu z kamery (szerokość)
                camera.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, imagePodglad.Height); //ustawienie właściwości obrazu z kamery (wysokość)
            }
            catch (Exception e) //złąp wyjątek obiektu
            {
                //Gdy jakiś fragment kodu wewnątrz bloku 'try' nie wykonał się poprawnie wtedy kod zostaje przerwany i program przeskakuje do bloku catch.

                MessageBox.Show(e.ToString());
                throw;
            }
            
            int czestotliwoscObraz = 30;
            timer1.Interval = 1000 / czestotliwoscObraz; //czestotliwosc odświerzania kamerki
            timer1.Enabled = true;
            
            double czestotliwoscAnaliza = 10;
            timer2.Interval = (int)(1000 / czestotliwoscAnaliza); //czestotliwosc analizy
            timer2.Enabled = true;

            pozycjaChwytakX = (imageRobot.Width) / 2;
            pozycjaChwytakY = (imageRobot.Height) / 2;
        }

        bool wczytany = false;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (checkBoxZPliku.Checked && wczytany == false)
            {
                zPliku();
                wczytany = true;
            }
            else if (checkBoxZPliku.Checked && wczytany == true) { }
            else
            {
                kamerka();
                wczytany = false;
            }

            if (checkBoxAuto.Checked) 
            {
                checkBoxZPliku.Enabled = false;
                checkBoxZPliku.Checked = false;
                wybranaOperacja = operacja.NOSIGNAL;
                radioButtonProgowanie.Enabled = false;
                radioButtonWyznaczSrodek.Enabled = false;
                radioButtonSygnaturaRadialna.Enabled = false;
            }
            else
            {
                checkBoxZPliku.Enabled = true;
                radioButtonProgowanie.Enabled = true;
                radioButtonWyznaczSrodek.Enabled = true;
                radioButtonSygnaturaRadialna.Enabled = true;
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if(wybranaOperacja==operacja.NOSIGNAL)
            {
                czysc_obraz(imageAnaliza, pictureBoxAnaliza);
            }
            if(!checkBoxAuto.Checked) operacjaWybrana();

            //System.Console.WriteLine("Operacja wybrana: " + wybranaOperacja);

            progowanie(); //funkcja wykonująca progowanie
            szerokosc(); //liczenie szerokości dłoni
            wysokosc(); //liczenie wysokości dłoni
            wyznaczSrodek(); //funkcja wyznaczająca środek cięzkości dłoni
            sygnaturaRadialnaWywolanie(); //sygnatura radialna

            //sugeruję tu zrobić podsumowanie powyżej wywołanych analiz i rozstrzygnięcie jaki gest jest pokazany
            //po wybraniu gestu zmiana symbolu w oknie pictureBoxGest na obrazek .png z folderu
            //jeśli gest ruchu - obliczenie odległości środka ręki od ekranu i wykonanie ruchu robota
            powiedzJakiToGest();

            rysujChwytak();

            for (int i = 0; i < 5; i++) // wyzerowanie warunków 
            {
                ktory_gest_wg_liczby_wierzcholkow[i] = false;
                ktory_gest_wg_powierzchni[i] = false;
                ktory_gest_wg_stosunku_szerokosc[i] = false;
                ktory_gest_wg_stosunku_dlugosc[i] = false;
            }
        }

        void kamerka()
        {
            Mat temp = camera.QueryFrame(); //klatka obrazu z kamery jest zapisana do temp
            CvInvoke.Resize(temp, imagePodglad, imagePodglad.Size); //dopasowanie wejścia temp do wyjścia imagePodglad
            pictureBoxPodgladKamery.Image = imagePodglad.Bitmap; //wyświetlenie bitmapy imagePodglad w oknie pictureBoxDuzeLewy
        }

        private void operacjaWybrana()
        {
            if (radioButtonProgowanie.Checked)
                wybranaOperacja = operacja.PROGOWANIE;
            else if (radioButtonWyznaczSrodek.Checked)
                wybranaOperacja = operacja.WYZNACZSRODEK;
            else if (radioButtonSygnaturaRadialna.Checked)
                wybranaOperacja = operacja.SYGNATURARADIALNA;
        }

        private void progowanie()
        {
            double BL, GL, RL;
            double BH, GH, RH;

            byte[,,] temp1 = imagePodglad.Data;
            byte[,,] temp2 = imageBufforProgowanie.Data;

            BL = 60;//12
            GL = 60;
            RL = 60;
            BH = 255;//102
            GH = 255;
            RH = 255;

            for (int x = 0; x < desired_image_size.Width; x++)
            {
                for (int y = 0; y < desired_image_size.Height; y++)
                {
                    if (temp1[y, x, 0] >= BL && temp1[y, x, 0] <= BH &&
                        temp1[y, x, 1] >= GL && temp1[y, x, 1] <= GH &&
                            temp1[y, x, 2] >= RL && temp1[y, x, 2] <= RH)
                    {
                        temp2[y, x, 0] = 255;
                        temp2[y, x, 1] = 255;
                        temp2[y, x, 2] = 255;
                    }
                    else
                    {
                        temp2[y, x, 0] = 0;
                        temp2[y, x, 1] = 0;
                        temp2[y, x, 2] = 0;
                    }
                }
            }

            imageBufforProgowanie.Data = temp2;
            
            if (wybranaOperacja == operacja.PROGOWANIE)
                pictureBoxAnaliza.Image = imageBufforProgowanie.Bitmap;
        }

        private void zPliku()
        {
            //@"F:\OneDrive - Politechnika Łódzka\SEMESTR 5\Systemy wizyjne\Systemy wizyjne (C#)\ProjektWizyjneV1\gestRuch.jpg"
            Mat temp = CvInvoke.Imread("gestStoj.jpg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
            CvInvoke.Resize(temp, imagePodglad, imagePodglad.Size); //dopasowanie wejścia temp do wyjścia image1
            pictureBoxPodgladKamery.Image = imagePodglad.Bitmap;
        }

        private void rysujChwytak()
        {
            imageRobot.SetValue(new MCvScalar(0,0,0));
            CvInvoke.Line(imageRobot, new Point(pozycjaChwytakX - 20, pozycjaChwytakY), new Point(pozycjaChwytakX + 20, pozycjaChwytakY), new MCvScalar(255, 0, 0), 3);
            CvInvoke.Line(imageRobot, new Point(pozycjaChwytakX - 20, pozycjaChwytakY), new Point(pozycjaChwytakX - 20, pozycjaChwytakY - 15), new MCvScalar(255, 0, 0), 3);
            CvInvoke.Line(imageRobot, new Point(pozycjaChwytakX + 20, pozycjaChwytakY), new Point(pozycjaChwytakX + 20, pozycjaChwytakY - 15), new MCvScalar(255, 0, 0), 3);
            CvInvoke.Line(imageRobot, new Point(pozycjaChwytakX, pozycjaChwytakY), new Point(pozycjaChwytakX, pozycjaChwytakY + 15), new MCvScalar(255, 0, 0), 3);
            //System.Console.WriteLine("Pozycja chwytaka: "+pozycjaChwytakX+", "+pozycjaChwytakY+"\n");
            labelPozycja.Text = pozycjaChwytakX+","+pozycjaChwytakY;
            pictureBoxRuchRobota.Image = imageRobot.Bitmap;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        void wyznaczSrodek()
        {
            //Reczne liczenie
            double F, Sx, Sy, x0, y0;
            double Jx0, Jy0, Jx0y0, Jx, Jy, Jxy, Je_0, Jt_0;
            double alfa_e, alfa_t, alfa_e_deg, alfa_t_deg;
            F = Sx = Sy = Jx0 = Jy0 = Jx0y0 = Jx = Jy = Jxy = Je_0 = Jt_0 = alfa_e = alfa_t = alfa_e_deg = alfa_t_deg = x0 = y0 = 0;

            //Odciecie ewentualnego stykania sie z krawedzia obrazu
            imageBufforAnaliza.Data = imageBufforProgowanie.Data; //kopia obrazu po progowaniu potrzebna do dalszej analizy 
            CvInvoke.Rectangle(imageBufforAnaliza, new Rectangle(0, 0, desired_image_size.Width, desired_image_size.Height), new MCvScalar(0, 0, 0), 2);
            byte[,,] temp = imageBufforAnaliza.Data;

            Application.DoEvents();

            //Wyliczenie momentow 1 i 2 stopnia

            for (int X = 0; X < desired_image_size.Width; X++)
            {
                for (int Y = 0; Y < desired_image_size.Height; Y++)
                {
                    if (temp[Y, X, 0] == 0xFF && temp[Y, X, 1] == 0xFF && temp[Y, X, 2] == 0xFF)
                    {
                        F = F + 1;
                        Sx = Sx + Y;
                        Sy = Sy + X;
                        Jx = Jx + Math.Pow(Y, 2);
                        Jy = Jy + Math.Pow(X, 2);
                        Jxy = Jxy + X * Y;
                    }
                }
            }

            //Obliczenie środka cieżkości
            if (F > 0)
            {
                x0 = Sy / F;
                y0 = Sx / F;
            }

            //Obliczenie momentów centralnych
            Jx0 = Jx - F * Math.Pow(y0, 2);
            Jy0 = Jy - F * Math.Pow(x0, 2);
            Jx0y0 = Jxy - F * x0 * y0;

            Je_0 = (Jx0 + Jy0) / 2 + Math.Sqrt(0.25 * Math.Pow(Jy0 - Jx0, 2) + Math.Pow(Jx0y0, 2));
            Jt_0 = (Jx0 + Jy0) / 2 - Math.Sqrt(0.25 * Math.Pow(Jy0 - Jx0, 2) + Math.Pow(Jx0y0, 2));

            if (Jy0 != Je_0)
                alfa_e = Math.Atan(Jx0y0 / (Jy0 - Je_0));
            else
                alfa_e = Math.PI / 2;

            if (Jy0 != Jt_0)
                alfa_t = Math.Atan(Jx0y0 / (Jy0 - Jt_0));
            else
                alfa_t = Math.PI / 2;

            //
            double[] wektor_czerw = new double[2];
            double[] wektor_nieb = new double[2];

            wektor_czerw[0] = Math.Cos(alfa_e);
            wektor_czerw[1] = Math.Sin(alfa_e);

            wektor_nieb[0] = Math.Cos(alfa_t);
            wektor_nieb[1] = Math.Sin(alfa_t);

            //Rysowanie punktów przeciecia
            Point P1, P2, P3, P4;

            P1 = new Point();
            P2 = new Point();
            P3 = new Point();
            P4 = new Point();
            Pc = new Point((int)x0, (int)y0);

            //System.Console.WriteLine("Pc: " + Pc.X + ", " + Pc.Y);
            labelPc.Text = Pc.X + "," + Pc.Y;

            bool czarny = false;
            int i, zakres;
            zakres = 320; //skąd to 320???? jeśli to od wymiaru obrazu to trzeba pobierać z rozmiaru okna a nie na sałe
            
            i = 0;
            while (czarny == false && i > -zakres && i < zakres)
            {
                int X = (int)(Pc.X + i * wektor_czerw[0]);
                int Y = (int)(Pc.Y + i * wektor_czerw[1]);
                if (temp[Y, X, 0] == 0)
                {
                    P1.X = X;
                    P1.Y = Y;
                    CvInvoke.Circle(imageBufforAnaliza, P1, 6, new MCvScalar(0, 0, 255), 2);
                    czarny = true;
                }
                i++;
            }

            i = 0;
            czarny = false;
            while (czarny == false && i > -zakres && i < zakres)
            {
                int X = (int)(Pc.X + i * wektor_czerw[0]);
                int Y = (int)(Pc.Y + i * wektor_czerw[1]);
                if (temp[Y, X, 0] == 0)
                {
                    P2.X = X;
                    P2.Y = Y;
                    CvInvoke.Circle(imageBufforAnaliza, P2, 6, new MCvScalar(0, 255, 0), 2);
                    czarny = true;
                }
                i--;
            }

            i = 0;
            czarny = false;
            while (czarny == false && i > -zakres && i < zakres)
            {
                int X = (int)(Pc.X + i * wektor_nieb[0]);
                int Y = (int)(Pc.Y + i * wektor_nieb[1]);
                if (temp[Y, X, 0] == 0)
                {
                    P3.X = X;
                    P3.Y = Y;
                    CvInvoke.Circle(imageBufforAnaliza, P3, 6, new MCvScalar(0, 255, 255), 2);
                    czarny = true;
                }
                i++;
            }

            i = 0;
            czarny = false;
            while (czarny == false && i > -zakres && i < zakres)
            {
                int X = (int)(Pc.X + i * wektor_nieb[0]);
                int Y = (int)(Pc.Y + i * wektor_nieb[1]);
                if (temp[Y, X, 0] == 0)
                {
                    P4.X = X;
                    P4.Y = Y;
                    CvInvoke.Circle(imageBufforAnaliza, P4, 6, new MCvScalar(255, 0, 255), 2);
                    czarny = true;
                }
                i--;
            }

            // Odleglosc od srodka ekranu 
            double odleglosc;
            odleglosc = Math.Sqrt(Math.Pow(Pc.X - imageRobot.Height / 2, 2) + Math.Pow(Pc.Y - imageRobot.Width / 2, 2));
            labelOdleglosc.Text = Math.Round(odleglosc).ToString();

            //Rysowanie okręgu w PC i wektorów
            CvInvoke.Circle(imageBufforAnaliza, Pc, 6, new MCvScalar(255, 0, 0), 2);

            CvInvoke.Line(imageBufforAnaliza, Pc, new Point((int)(Pc.X + 120), (int)(Pc.Y)), new MCvScalar(0, 255, 0), 2);
            CvInvoke.Line(imageBufforAnaliza, Pc, new Point((int)(Pc.X + 100 * wektor_czerw[0]), (int)(Pc.Y + 100 * wektor_czerw[1])), new MCvScalar(0, 0, 255), 2);
            CvInvoke.Line(imageBufforAnaliza, Pc, new Point((int)(Pc.X + 100 * wektor_nieb[0]), (int)(Pc.Y + 100 * wektor_nieb[1])), new MCvScalar(255, 0, 0), 2);

            imageBufforAnaliza.Data = temp;

            if (wybranaOperacja == operacja.WYZNACZSRODEK)
            {
                imageAnaliza.Data = imageBufforAnaliza.Data;
                pictureBoxAnaliza.Image = imageAnaliza.Bitmap;
            }
                
            labelPowierzchnia.Text = F.ToString();

            //Punktacja odnosnie metod analizy
            if (F >= 37000 && F < 48000)
            {
                ktory_gest_wg_powierzchni[2] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonPowOtworz.BackColor = Color.Lime;
            }
            else if (F >= 25000 && F < 37000)
            {
                ktory_gest_wg_powierzchni[0] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                ktory_gest_wg_powierzchni[1] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                ktory_gest_wg_powierzchni[3] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonPowRuch.BackColor = Color.Lime;
                buttonPowStop.BackColor = Color.Lime;
                buttonPowZamknij.BackColor = Color.Lime;
            }
            else
            {
                ktory_gest_wg_powierzchni[4] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonPowOtworz.BackColor = Color.DarkGray;
                buttonPowRuch.BackColor = Color.DarkGray;
                buttonPowStop.BackColor = Color.DarkGray;
                buttonPowZamknij.BackColor = Color.DarkGray;
                
            }
        }

        private void przesunChwytak()
        {
            //Obliczanie przesunięcia chwytaka
            int odlX, odlY;
            int offsetReki = 30; //offset na Y bo jak ręka jest na środku to środek ciężkości jest poniżej i ucieka w dół (+ ciężko tak ustawić dłoń żeby wywołać ruch w górę - głównieprzez nadgarstek itp.)
            odlX = Pc.X - imageRobot.Width / 2;
            odlY = Pc.Y - (imageRobot.Height / 2 + offsetReki);

            //System.Console.WriteLine("Odl. PC od śr.: " + odlX + ", " + odlY);
            labelOdlegloscXY.Text = odlX + "," + odlY;

            pozycjaChwytakX += odlX / 8;
            pozycjaChwytakY += odlY / 8;

            //Zabezpieczenie przed wyjściem poza ekran
            if (pozycjaChwytakX >= imageRobot.Width) pozycjaChwytakX = imageRobot.Width;
            if (pozycjaChwytakX <= 0) pozycjaChwytakX = 0;
            if (pozycjaChwytakY >= imageRobot.Height) pozycjaChwytakY = imageRobot.Height;
            if (pozycjaChwytakY <= 0) pozycjaChwytakY = 0;
        }

        private void powiedzJakiToGest()
        {
            //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
            int nr_wytypowanego_gestu = 0;

            if (ktory_gest_wg_liczby_wierzcholkow[4] == true || ktory_gest_wg_powierzchni[4] == true || ktory_gest_wg_stosunku_szerokosc[4] == true || ktory_gest_wg_stosunku_dlugosc[4] == true)
            {
                nr_wytypowanego_gestu = 4; // gest niejednoznaczny  
            }
            else
            {
                if (ktory_gest_wg_liczby_wierzcholkow[0] == true && ktory_gest_wg_powierzchni[0] == true && ktory_gest_wg_stosunku_szerokosc[0] == true && ktory_gest_wg_stosunku_dlugosc[0] == true)
                {
                    nr_wytypowanego_gestu = 0;
                }
                else if (ktory_gest_wg_liczby_wierzcholkow[1] == true && ktory_gest_wg_powierzchni[1] == true && ktory_gest_wg_stosunku_szerokosc[1] == true && ktory_gest_wg_stosunku_dlugosc[1] == true)
                {
                    nr_wytypowanego_gestu = 1;
                }
                else if (ktory_gest_wg_liczby_wierzcholkow[2] == true && ktory_gest_wg_powierzchni[2] == true && ktory_gest_wg_stosunku_szerokosc[2] == true && ktory_gest_wg_stosunku_dlugosc[2] == true)
                {
                    nr_wytypowanego_gestu = 2;
                }
                else if (ktory_gest_wg_liczby_wierzcholkow[3] == true && ktory_gest_wg_powierzchni[3] == true && ktory_gest_wg_stosunku_szerokosc[3] == true && ktory_gest_wg_stosunku_dlugosc[3] == true)
                {
                    nr_wytypowanego_gestu = 3;
                }
                else
                {
                    nr_wytypowanego_gestu = 4;
                }

            }

            switch (nr_wytypowanego_gestu)
            {
                case 0: // gest ruchu

                    Mat temp_ruch = CvInvoke.Imread("gestRuch.jpg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
                    CvInvoke.Resize(temp_ruch, imageWykrytyGest, imageWykrytyGest.Size); //dopasowanie wejścia temp do wyjścia image1
                    pictureBoxGest.Image = imageWykrytyGest.Bitmap;
                    przesunChwytak();
                    break;
                case 1: //gest zatrzymania

                    Mat temp_zatrzymanie = CvInvoke.Imread("gestStoj.jpg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
                    CvInvoke.Resize(temp_zatrzymanie, imageWykrytyGest, imageWykrytyGest.Size); //dopasowanie wejścia temp do wyjścia image1
                    pictureBoxGest.Image = imageWykrytyGest.Bitmap;
                    break;
                case 2: //gest otwarcia chwytaka

                    Mat temp_otworz = CvInvoke.Imread("gestOtworz.jpg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
                    CvInvoke.Resize(temp_otworz, imageWykrytyGest, imageWykrytyGest.Size); //dopasowanie wejścia temp do wyjścia image1
                    pictureBoxGest.Image = imageWykrytyGest.Bitmap;
                    labelChwytak.Text = "Otwarty";
                    break;

                case 3: //gest zamkniecia chwytaka

                    Mat temp_zamknij = CvInvoke.Imread("gestZamknij.jpg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
                    CvInvoke.Resize(temp_zamknij, imageWykrytyGest, imageWykrytyGest.Size); //dopasowanie wejścia temp do wyjścia image1
                    pictureBoxGest.Image = imageWykrytyGest.Bitmap;
                    labelChwytak.Text = "Zamknięty";
                    break;

                case 4:
                    Mat temp_brak = CvInvoke.Imread("Idk.jpeg"); //klatka obrazu z pliku z podanej ścieżki jest zapisana do temp
                    CvInvoke.Resize(temp_brak, imageWykrytyGest, imageWykrytyGest.Size); //dopasowanie wejścia temp do wyjścia image1
                    pictureBoxGest.Image = imageWykrytyGest.Bitmap;
                    break;

                default:
                    break;
            }

        }

        private void sygnaturaRadialnaWywolanie()
        {
            //ustalenie punktu dla którego będziemy ryowali sygnaturę
            Point punkt;
            //punkt = new Point(desired_image_size.Width/2, desired_image_size.Height/2); //względem śr. ekranu

            int offset = 60; //110 dla kciuka, około 30 dla reszty gestów

            if((Pc.Y+offset)<desired_image_size.Height) //względem Pc ręki
                punkt = new Point(Pc.X, Pc.Y+offset ); 
                //offset +30 (czyli pkt o 30pix niżej bo słabo wykrywa palec - taka sama długośc jest do nadgarstka od Pc - wiadać kilka górek)
            else
                punkt = new Point(Pc.X, Pc.Y);

            tabelaPromieni = sygnatura_radialna(punkt);

            czysc_obraz(imageWykres, pictureBoxWykres); //czyszczenie obrazu wykresu

            namaluj_dane_z_tabeli(tabelaPromieni, null, new MCvScalar(255, 0, 0), TrybRysowania.TYLKO_DANE);

            usrednianie_wykresu();

            licz_wierzcholki(tabelaPromieni, tabelaWartosciSrednich);
        }

        private double[] sygnatura_radialna(Point start)
        {
            MCvScalar kolor_promienia = new MCvScalar();
            double[,] katy_kolejnych_promieni = new double[liczba_promieni, 2];
            double[] promienie = new double[liczba_promieni];
            double krok_katowy, aktualny_kat;

            //generuj_losowy_kolor(ref kolor_promienia);
            kolor_promienia.V0 = 255;
            kolor_promienia.V1 = 0;
            kolor_promienia.V2 = 0;

            aktualny_kat = kat_poczatkowy * (Math.PI / 180);

            /*
            if (radioButton_Draw_clockwise.Checked)
                krok_katowy = (2 * Math.PI / liczba_promieni);
            else
                krok_katowy = -(2 * Math.PI / liczba_promieni);
            */
            krok_katowy = (2 * Math.PI / liczba_promieni);

            for (int i = 0; i < liczba_promieni; i++)
            {
                katy_kolejnych_promieni[i, 0] = Math.Cos(aktualny_kat);
                katy_kolejnych_promieni[i, 1] = Math.Sin(aktualny_kat);
                aktualny_kat += krok_katowy;
            }

            
            //Odciecie ewentualnego stykania sie z krawedzia obrazu W ORYGINALE BUFFOR!!! (wyniku progowania)
            CvInvoke.Rectangle(imageBufforProgowanie, new Rectangle(0, 0, desired_image_size.Width, desired_image_size.Height), new MCvScalar(0, 0, 0), 2);

            imageBufforAnaliza.SetZero(); //czyszczenie imageBufforAnaliza - bufforu z wynikiem
            byte[,,] temp1 = imageBufforProgowanie.Data;
            int zakres = (int)Math.Sqrt(Math.Pow(desired_image_size.Width, 2) + Math.Pow(desired_image_size.Height, 2)); //dł. przekątnej obrazu
            
            for (int p = 0; p < liczba_promieni; p++) //iteracja po ilości promieni
            {
                for (int d = 0; d < zakres; d++) //iteracja po prostej dł. przekątnej obrazu
                {
                    Point cp = new Point();
                    int dx, dy;
                    dx = (int)(d * katy_kolejnych_promieni[p, 0]);
                    dy = (int)(d * katy_kolejnych_promieni[p, 1]);
                    if (Math.Abs(dx) < zakres && Math.Abs(dy) < zakres)
                    {
                        cp.X = start.X + dx;
                        cp.Y = start.Y + dy;
                        if (temp1[cp.Y, cp.X, 0] == 0x00) //0x00
                        {
                            CvInvoke.Line(imageBufforAnaliza, start, cp, kolor_promienia, 1);
                            promienie[p] = Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
                            break;
                        }
                    }
                }
            }

            if (wybranaOperacja == operacja.SYGNATURARADIALNA)
            {
                imageAnaliza.Data = imageBufforAnaliza.Data;
                pictureBoxAnaliza.Image = imageAnaliza.Bitmap;
            }
                

            return promienie;
        }

        private void czysc_obraz(Image<Bgr, byte> im, PictureBox PB)
        {
            im.SetZero();
            PB.Image = im.Bitmap;
        }

        private void namaluj_dane_z_tabeli(double[] dane, double[] krzywa, MCvScalar kolor, TrybRysowania tryb)
        {
            int w, h;
            int rX, rY, rW, rH;
            double sX, sY;
            w = pictureBoxWykres.Width;
            h = pictureBoxWykres.Height;
            int margines_na_tekst = 40;
            sX = ((double)w / (double)liczba_promieni);//Dopasowanie szerokości
            sY = (((double)h - margines_na_tekst) / Math.Max(dane.Max(), 10));//Dopasowanie wysokości
            rX = rY = rW = rH = 0;

            if (tryb != TrybRysowania.TYLKO_KRZYWA)
            {
                for (int p = 0; p < liczba_promieni; p++)
                {
                    Rectangle r;
                    rX = (int)(sX * (double)p);
                    rW = ((int)(sX * (double)(p + 1))) - rX;

                    //Wybor rysowania
                    if (tryb == TrybRysowania.NAD_KRZYWA)
                    {
                        rY = (int)(h - sY * dane[p]);
                        rH = (int)((dane[p] - krzywa[p]) * sY) + 1;
                        if (rH < 0)
                            continue;
                    }
                    else if (tryb == TrybRysowania.TYLKO_DANE)
                    {
                        rY = (int)(h - sY * dane[p]);
                        rH = (int)(sY * dane[p]);
                    }

                    r = new Rectangle(rX, rY, rW, rH);
                    CvInvoke.Rectangle(imageWykres, r, kolor, -1);
                }
            }
            else
            {
                for (int p = 0; p < liczba_promieni - 1; p++)
                {
                    Point P1, P2;
                    int curr_x, next_x;
                    curr_x = (int)(sX * (double)p);
                    next_x = (int)(sX * (double)(p + 1));
                    P1 = new Point(curr_x, (int)(h - (int)(sY * krzywa[p])));
                    P2 = new Point(next_x, (int)(h - (int)(sY * krzywa[p + 1])));
                    CvInvoke.Line(imageWykres, P1, P2, kolor, 1);
                }
            }

            pictureBoxWykres.Image = imageWykres.Bitmap;
        }

        private void usrednianie_wykresu()
        {
            MCvScalar kolor_nad_srednia = new MCvScalar(0, 255, 255);

            tabelaWartosciSrednich = wylicz_srednia_z_sygnatury(tabelaPromieni);

            kolor_nad_srednia = new MCvScalar(255, 0, 255);
            
            namaluj_dane_z_tabeli(tabelaPromieni, tabelaWartosciSrednich, kolor_nad_srednia, TrybRysowania.NAD_KRZYWA);
            namaluj_dane_z_tabeli(tabelaPromieni, tabelaWartosciSrednich, kolor_nad_srednia, TrybRysowania.TYLKO_KRZYWA);
        }

        private double[] wylicz_srednia_z_sygnatury(double[] data)
        {
            double[] srednia = new double[data.Length];
            
            double avg = (data.Max() + data.Min()) / 2.0;

            //System.Console.WriteLine("avg" + (int)avg + "max" + (int)data.Max() + "roznica" + (int)(data.Max()- avg));

            for (int i = 0; i < data.Length; i++)
            {
                srednia[i] = avg;
            }
            
            return srednia;
        }

        private int modulo(int a, int b)
        {
            return (Math.Abs(a * b) + a) % b;
        }

        private void licz_wierzcholki(double[] dane, double[] krzywa)
        {
            double sX;
            int przeskok = (liczba_promieni / 30); //15
            int wierzcholki = 0;
            sX = ((double)pictureBoxWykres.Width / (double)liczba_promieni);//Dopasowanie szerokości

            for (int i = 0; i < liczba_promieni - 1; i++)
            {
                if (dane[i] < krzywa[i] && dane[i + 1] >= krzywa[i + 1])
                {
                    wierzcholki++;
                    CvInvoke.Line(imageWykres, new Point((int)(i * sX), pictureBoxWykres.Height), new Point((int)(i * sX), 40), new MCvScalar(0, 255, 0), 1);
                    i += przeskok;
                    CvInvoke.Line(imageWykres, new Point((int)(i * sX), pictureBoxWykres.Height), new Point((int)(i * sX), 50), new MCvScalar(255, 255, 0), 1);
                }
            }
            labelIleWierzcholkow.Text = wierzcholki.ToString();
            pictureBoxWykres.Image = imageWykres.Bitmap;

            //jeśli max>sredniej gest stop
            //else max>>sredniej switch
            double avg = (tabelaPromieni.Max() + tabelaPromieni.Min()) / 2.0;

            if (tabelaPromieni.Max()-avg<80)
            {
                ktory_gest_wg_liczby_wierzcholkow[1] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonLWStop.BackColor = Color.Lime;
            }
            else
                switch (wierzcholki)
                {
                    case 1: // gest ruchu lub stop
                        ktory_gest_wg_liczby_wierzcholkow[0] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                        
                        buttonLWRuch.BackColor = Color.Lime;
                        break;

                    case 2: // gest zamkniecia chwytaka
                        ktory_gest_wg_liczby_wierzcholkow[3] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                        buttonLWZamknij.BackColor = Color.Lime;
                        break;

                    case 5: // gest otwarcia chwytaka   
                        ktory_gest_wg_liczby_wierzcholkow[2] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                        buttonLWOtworz.BackColor = Color.Lime;
                        break;

                    default: // gest niejednoznaczny
                        ktory_gest_wg_liczby_wierzcholkow[4] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                        buttonLWRuch.BackColor = Color.DarkGray;
                        buttonLWStop.BackColor = Color.DarkGray;
                        buttonLWZamknij.BackColor = Color.DarkGray;
                        buttonLWOtworz.BackColor = Color.DarkGray;
                        break;
                }
        }
        
        private void szerokosc()
        {
            byte[,,] temp = imageBufforProgowanie.Data;

            int krawedzLewa = 0;
            int krawedzPrawa = desired_image_size.Width;
            bool lznal = false;
            bool pznal = false;
            int ileBialychLewy = 0;
            int ileBialychPrawy = 0;

            for (int x = 0; x < desired_image_size.Width/2; x++)
            {
                for (int y = 0; y < desired_image_size.Height; y++)
                {
                    if (temp[y, x, 0] == 255) ileBialychLewy++;

                    if (temp[y, (desired_image_size.Width - x - 1), 0] == 255) ileBialychPrawy++;

                }

                if (lznal == false && ileBialychLewy > 20)
                {
                    krawedzLewa = x;
                    lznal = true;
                }
                if (pznal == false && ileBialychPrawy > 20)
                {
                    krawedzPrawa = desired_image_size.Width - x - 1;
                    pznal = true;
                }

                ileBialychLewy = 0;
                ileBialychPrawy = 0;

                if (lznal && pznal) break;
            }

            //rysowanie pionowych linni obrazujących wynik mierzenia szerokości
            CvInvoke.Line(imageBufforProgowanie, new Point(krawedzLewa, 0), new Point(krawedzLewa, desired_image_size.Height), new MCvScalar(255, 255, 0), 1);
            CvInvoke.Line(imageBufforProgowanie, new Point(krawedzPrawa, 0), new Point(krawedzPrawa, desired_image_size.Height), new MCvScalar(255, 255, 0), 1);

            int szerokosc = krawedzPrawa - krawedzLewa;

            labelSzerokosc.Text = ""+szerokosc;

            //Punktacja odnosnie metod analizy
            if (szerokosc >= 220 && szerokosc < 330)
            {
                ktory_gest_wg_stosunku_szerokosc[2] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonSzerOtworz.BackColor = Color.Lime;
            }
            else if (szerokosc >= 150 && szerokosc < 220)
            {
                ktory_gest_wg_stosunku_szerokosc[0] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                ktory_gest_wg_stosunku_szerokosc[1] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                ktory_gest_wg_stosunku_szerokosc[3] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonSzerRuch.BackColor = Color.Lime;
                buttonSzerStop.BackColor = Color.Lime;
                buttonSzerZamknij.BackColor = Color.Lime;
            }
            else
            {
                ktory_gest_wg_stosunku_szerokosc[4] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonSzerOtworz.BackColor = Color.DarkGray;
                buttonSzerRuch.BackColor = Color.DarkGray;
                buttonSzerStop.BackColor = Color.DarkGray;
                buttonSzerZamknij.BackColor = Color.DarkGray;
            }
        }

        

        private void wysokosc()
        {
            byte[,,] temp = imageBufforProgowanie.Data;

            int krawedzGora = 0;
            bool kznal = false;
            int ileBialych = 0;

            for (int y = 0; y < desired_image_size.Height / 2; y++)
            {
                for (int x = 0; x < desired_image_size.Width; x++)
                {
                    if (temp[y, x, 0] == 255) ileBialych++;
                }

                if (kznal == false && ileBialych > 20)
                {
                    krawedzGora = y;
                    kznal = true;
                }

                ileBialych = 0;

                if (kznal) break;
            }

            //rysowanie poziomej linni obrazującyej wynik mierzenia wysokosci
            CvInvoke.Line(imageBufforProgowanie, new Point(0, krawedzGora), new Point(desired_image_size.Width, krawedzGora), new MCvScalar(255, 255, 0), 1);
            //CvInvoke.Line(imageBufforProgowanie, new Point(krawedzPrawa, 0), new Point(krawedzPrawa, desired_image_size.Height), new MCvScalar(255, 255, 0), 1);

            int wysokosc = Pc.Y - krawedzGora;

            labelWysokosc.Text = "" + wysokosc;

            //Punktacja odnosnie metod analizy
            if (wysokosc >= 150 && wysokosc < 270)
            {
                ktory_gest_wg_stosunku_dlugosc[0] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny
                ktory_gest_wg_stosunku_dlugosc[2] = true;
                ktory_gest_wg_stosunku_dlugosc[3] = true;

                buttonWysRuch.BackColor = Color.Lime;
                buttonWysOtworz.BackColor = Color.Lime;
                buttonWysZamknij.BackColor = Color.Lime;
            }
            else if (wysokosc >= 80 && wysokosc < 120)
            {
                ktory_gest_wg_stosunku_dlugosc[1] = true;

                buttonWysStop.BackColor = Color.Lime;
            }
            else
            {
                ktory_gest_wg_stosunku_dlugosc[4] = true; //0 - ruch, 1 -  stop, 2 - otwarcie, 3 - zamkniecie, 4 - niejednoznaczny

                buttonWysRuch.BackColor = Color.DarkGray;
                buttonWysOtworz.BackColor = Color.DarkGray;
                buttonWysZamknij.BackColor = Color.DarkGray;
                buttonWysStop.BackColor = Color.DarkGray;
            }
        }
        
    }

    
}