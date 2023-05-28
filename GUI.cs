using NeuroSky.ThinkGear;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections;
using System.Security.Authentication.ExtendedProtection;

namespace HelloEEG
{
    public partial class GUI : Form
    {
        static ArrayList attention = new ArrayList();
        static ArrayList meditation = new ArrayList();
        static ArrayList blink = new ArrayList();

        static Connector connector;
        static byte poorSig;
        private string password;

        public GUI(string password)
        {
            InitializeComponent();
            this.password = password;
            pw.Text = password;
            _ = Brain();
        }


        public async Task Brain()
        {
            UpdateTextBox("Connecting code: " + password);
            //get uri 생성
            var getUri = "http://localhost:8080/api/userInfo/";
            getUri += password;
            UpdateTextBox("getUri: " + getUri);

            int blinkArrayCount = 0; // blink array 슬라이딩윈도우 세기
            int blinkThreshold = 50; // 몇 이상이면 눈을 깜빡였다고 판단할 것인가
            int blinkCount = 0; // 슬라이딩윈도우 내에서 눈을 깜빡인 횟수가 몇번인지 (5번 중에 3번 이상이면 설문 종료하기)

            while (true)
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetAsync(getUri);
                        await Task.Delay(3000);
                        UpdateTextBox("Got uri response!");
                        var content = await response.Content.ReadAsStringAsync();
                        // Deserialize the JSON string into a dynamic object
                        dynamic obj1 = JsonConvert.DeserializeObject(content);

                        if (obj1 == null) UpdateTextBox("Failed to get content. Please input the connection code.");

                        Console.WriteLine(obj1);
                        // Access the individual properties of the object and print them
                        UpdateTextBox("Surveying: " + obj1.flag);
                        Console.WriteLine("Flag: " + obj1.flag);

                        while (obj1.flag == true)
                        {
                            connector = new Connector();
                            connector.DeviceConnected += new EventHandler(OnDeviceConnected);
                            connector.DeviceConnectFail += new EventHandler(OnDeviceFail);
                            connector.DeviceValidating += new EventHandler(OnDeviceValidating);

                            // Scan for devices across COM ports
                            // The COM port named will be the first COM port that is checked.
                            connector.ConnectScan("COM6");


                            // Blink detection needs to be manually turned on
                            connector.setBlinkDetectionEnabled(true);
                            while (true)
                            {
                                response = await client.GetAsync(getUri);
                                content = await response.Content.ReadAsStringAsync();
                                obj1 = JsonConvert.DeserializeObject(content);

                                if (blink.Count > 5)
                                {
                                    for (int i = blinkArrayCount; i >= blinkArrayCount-4; i--)
                                    {
                                        if ((int)blink[i] > blinkThreshold)
                                        {
                                            blinkCount++;
                                        }
                                    }
                                    
                                    if (blinkCount >= 3)
                                    {
                                        obj1.flag = false;
                                    }
                                }
                                blinkArrayCount += 1;

                                if (obj1.flag == false)
                                {
                                    drawChart form1 = new drawChart();

                                    Series seriesA = new Series("Attention");
                                    Series seriesM = new Series("Meditation");


                                    // Chart를 Line Chart로 설정
                                    seriesA.ChartType = SeriesChartType.Line;


                                    // 처음 측정을 시작하면 4개정도 0으로 받아지므로 앞부분 4개 삭제
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (attention.Count > 0) { attention.RemoveAt(0); }
                                        if (meditation.Count > 0) { meditation.RemoveAt(0);}
                                    }

                                    foreach (object obj in attention)
                                    {
                                        seriesA.Points.Add((double)obj);
                                    }

                                    foreach (object obj in meditation)
                                    {
                                        seriesM.Points.Add((double)obj);
                                    }

                                    form1.chart1.Series.Add(seriesA);
                                    form1.chart1.Series.Add(seriesM);

                                    // Chart를 Line Chart로 설정합니다.
                                    seriesA.ChartType = SeriesChartType.Line;
                                    seriesM.ChartType = SeriesChartType.Line;

                                    form1.chart1.SaveImage("C:\\Users\\USER\\Desktop\\NeuroSky MindWave Mobile_Example_HelloEEG\\chart.png", ChartImageFormat.Png);

                                    UpdateTextBox(">> Connection closed. Bye.");
                                    //System.Console.WriteLine("Goodbye.");
                                    connector.Close();

                                    // 집중도, 안정도 평균 구하기
                                    float sumAttention = 0;
                                    float sumMeditation = 0;

                                    foreach (int num in attention) { sumAttention += num; }
                                    foreach (int num in meditation) { sumMeditation += num; }

                                    float avgAttention = sumAttention / attention.Count;
                                    float avgMeditation = sumMeditation / meditation.Count;

                                    // POST
                                    var postUri = new Uri("http://localhost:8080/api/imgInfo");

                                    var data = new { memberId = obj1.memberId, surveyId = obj1.surveyId, code = obj1.code, avgAtt = obj1.avgAttention, avgMed = obj1.avgMeditation };

                                    var imageContent = new ByteArrayContent(File.ReadAllBytes("C:\\Users\\USER\\Desktop\\NeuroSky MindWave Mobile_Example_HelloEEG\\chart.png"));
                                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                                    var jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                    var jsonContent = new StringContent(jsonData, Encoding.UTF8, "application/json");
                                    //gui.UpdateTextBox(jsonData);
                                    Console.WriteLine(jsonData);

                                    var mergedContent = new MultipartFormDataContent();
                                    mergedContent.Add(jsonContent, "braindata");
                                    mergedContent.Add(imageContent, "image", "image.png");

                                    //Console.WriteLine(mergedContent);
                                    //Console.WriteLine(mergedContent.Headers);
                                    var response2 = await client.PostAsync(postUri, mergedContent);
                                    var result = await response2.Content.ReadAsStringAsync();
                                }
                            }


                        }


                    }
                    catch
                    {
                        UpdateTextBox("Trying to connect...");
                        continue; 
                    }
                }
            }
        }

        // Called when a device is connected 
        static void OnDeviceConnected(object sender, EventArgs e)
        {
            Connector.DeviceEventArgs de = (Connector.DeviceEventArgs)e;

            Console.WriteLine("Device found on: " + de.Device.PortName);

            de.Device.DataReceived += new EventHandler(OnDataReceived);
        }




        // Called when scanning fails

        static void OnDeviceFail(object sender, EventArgs e)
        {
            Console.WriteLine("No devices found! :(");
        }



        // Called when each port is being validated

        static void OnDeviceValidating(object sender, EventArgs e)
        {
            Console.WriteLine("Validating: ");
        }


        // Called when data is received from a device

        static void OnDataReceived(object sender, EventArgs e)
        {

            //Device d = (Device)sender;

            Device.DataEventArgs de = (Device.DataEventArgs)e;
            NeuroSky.ThinkGear.DataRow[] tempDataRowArray = de.DataRowArray;

            TGParser tgParser = new TGParser();

            tgParser.Read(de.DataRowArray);

            /* Loops through the newly parsed data of the connected headset*/
            // The comments below indicate and can be used to print out the different data outputs. 

            for (int i = 0; i < tgParser.ParsedData.Length; i++)
            {

                if (tgParser.ParsedData[i].ContainsKey("Raw"))
                {

                    //Console.WriteLine("Raw Value:" + tgParser.ParsedData[i]["Raw"]);

                }

                if (tgParser.ParsedData[i].ContainsKey("PoorSignal"))
                {

                    //The following line prints the Time associated with the parsed data
                    //Console.WriteLine("Time:" + tgParser.ParsedData[i]["Time"]);

                    //A Poor Signal value of 0 indicates that your headset is fitting properly
                    //Console.WriteLine("Poor Signal:" + tgParser.ParsedData[i]["PoorSignal"]);

                    if (tgParser.ParsedData[i]["PoorSignal"] > 50)
                    {
                        Console.WriteLine("Poor SIGNAL!");
                    }

                    poorSig = (byte)tgParser.ParsedData[i]["PoorSignal"];
                }


                if (tgParser.ParsedData[i].ContainsKey("Attention"))
                {
                    Console.WriteLine("Att Value:" + tgParser.ParsedData[i]["Attention"]);

                    attention.Add(tgParser.ParsedData[i]["Attention"]);
                }


                if (tgParser.ParsedData[i].ContainsKey("Meditation"))
                {
                    Console.WriteLine("Med Value:" + tgParser.ParsedData[i]["Meditation"]);

                    meditation.Add(tgParser.ParsedData[i]["Meditation"]);
                }


                if (tgParser.ParsedData[i].ContainsKey("EegPowerDelta"))
                {
                    //Console.WriteLine("Delta: " + tgParser.ParsedData[i]["EegPowerDelta"]);

                }

                if (tgParser.ParsedData[i].ContainsKey("BlinkStrength"))
                {
                    //Console.WriteLine("Eyeblink " + tgParser.ParsedData[i]["BlinkStrength"]);

                    blink.Add(tgParser.ParsedData[i]["BlinkStrength"]);
                }


            }

        }
        private void gui_Load(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {


            // 클립보드에 접근하여 비동기로 복사 작업 실행
            Clipboard.SetText(password);
            // 복사 작업이 완료되면 메시지를 보여줌
            MessageBox.Show("연결 코드가 클립보드에 복사되었습니다.\n뇌파 측정 기기와의 연결을 위해 웹페이지에 코드를 입력해주세요!");
            UpdateTextBox("Connection code copied to clipboard");
        }

        private void logBox_TextChanged(object sender, EventArgs e)
        {

        }

        public void UpdateTextBox(string text)
        {
            logBox.Text += ">> " + text + Environment.NewLine;
        }
    }
}
