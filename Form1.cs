using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;


namespace FTP_Server_Project
{
    public partial class Form1 : Form
    {
        TcpListener server = null; // 서버
        TcpClient clientSocket = null; // 소켓

        string date; // 날짜
        private Dictionary<TcpClient, string> clientList = new Dictionary<TcpClient, string>(); // 각 클라이언트 마다 리스트에 추가
        private int PORT = 21; // 포트 정보
     
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitForm();
            this.Text = "Server";
            connectIPBox.Text = "";
            connectIPBox.Text = GetLocalIP();
        }
        
        // 폼 초기화
        private void InitForm()
        {
            connectIPBox.Text = "";
            connectIDBox.Text = "";
            connectPWBox.Text = "";
            richTextBox1.Text = "";
            listBox1.Items.Clear();
        }


        // 로컬IP 가져오기
        private string GetLocalIP()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string localIP = string.Empty;

            for (int i = 0; i < host.AddressList.Length; i++)
            {
                if (host.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = host.AddressList[i].ToString();
                    break;
                }
            }

            return localIP;
        }

        // 서버 실행 버튼
        private void button3_Click(object sender, EventArgs e)
        {
            Thread thread1 = new Thread(InitSocket);
            thread1.IsBackground = true;
            thread1.Start();
        }

        // 소켓 초기화
        private void InitSocket()
        {
            server = new TcpListener(IPAddress.Parse(connectIPBox.Text), PORT); // 서버 객체 생성 및 IP주소와 Port번호를 할당
            clientSocket = default(TcpClient); // 소켓 설정
            server.Start(); // 서버 시작
            DisplayText("System : Server 시작");

            while (true)
            {
                try
                {
                    clientSocket = server.AcceptTcpClient(); // client 소켓 접속 허용
                    NetworkStream stream = clientSocket.GetStream();

                    // 클라이언트로부터 ID, PW 받기
                    byte[] buffer = new byte[1024]; // 버퍼
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    string credentials = Encoding.Unicode.GetString(buffer, 0, bytes);
                    string[] credentialParts = credentials.Split(new char[] { '$' }, 2);
                    string id = credentialParts[0]; // client 사용자 명
                    string password = credentialParts[1]; // client 비밀번호

                    // 인증 확인
                    if (!AuthenticateUser(id, password))
                    {
                        byte[] response = Encoding.Unicode.GetBytes("Invalid credentials");
                        stream.Write(response, 0, response.Length);
                        stream.Flush();
                        clientSocket.Close();
                        continue; // 다음 클라이언트 접속 대기
                    }
                    byte[] successResponse = Encoding.Unicode.GetBytes("Authentication successful");
                    stream.Write(successResponse, 0, successResponse.Length);
                    stream.Flush();

                    clientList.Add(clientSocket, id); // cleint 리스트에 추가
                    ///SendMessageAll(id + " 님이 입장하셨습니다.", "", false); // 모든 client에게 메세지 전송
                    SetUserList(id, "I");

                    // 폴더 목록 전송
                    SendFolderList(stream);

                    HandleClient h_client = new HandleClient(); // 클라이언트 추가
                    h_client.OnReceived += new HandleClient.MessageDisplayHandler(OnReceived);
                    h_client.OnDisconnected += new HandleClient.DisconnectedHandler(h_client_OnDisconnected);
                    h_client.startClient(clientSocket, clientList);
                }
                catch (SocketException se) { break; }

                catch (Exception ex) { break; }
            }

            clientSocket.Close(); // client 소켓 닫기

            server.Stop(); // 서버 종료
        }

        // 폴더 및 파일 목록 전송 함수
        // 폴더 목록 전송 함수
        private void SendFolderList(NetworkStream stream)
        {
            //string folderPath = "C:\\Users\\User\\Pictures";
            //try
            //{
            //    List<string> folderAndFileList = new List<string>();
            //    GetFoldersAndFiles(folderPath, folderAndFileList);

            //    string folderListJson = JsonConvert.SerializeObject(folderAndFileList);
            //    byte[] buffer = Encoding.Unicode.GetBytes(folderListJson);
            //    stream.Write(buffer, 0, buffer.Length);
            //    stream.Flush();
            //}
            //catch (Exception ex)
            //{
            //    DisplayText("Error: " + ex.Message);
            //    byte[] buffer = Encoding.Unicode.GetBytes("Error: An error occurred while accessing the folder.");
            //    stream.Write(buffer, 0, buffer.Length);
            //    stream.Flush();
            //}

            // 드라이브 정보 가져오기
            string driveInfoJson = GetDriveInfoAsJson();
            
            // JSON 문자열을 바이트 배열로 변환
            byte[] data = Encoding.UTF8.GetBytes(driveInfoJson);
            
            // 데이터 전송
            stream.Write(data, 0, data.Length);  
            stream.Flush();
        }
        private string GetDriveInfoAsJson()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            return JsonConvert.SerializeObject(drives);
        }

        private void GetFoldersAndFiles(string path, List<string> list)
        {
            try
            {
                string[] directories = Directory.GetDirectories(path);
                string[] files = Directory.GetFiles(path);

                foreach (string directory in directories)
                {
                    list.Add(directory);
                    GetFoldersAndFiles(directory, list); // 재귀적으로 하위 폴더의 내용 추가
                }

                foreach (string file in files)
                {
                    list.Add(file);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // 접근 권한이 없는 폴더가 있을 경우 무시
                DisplayText("Access denied: " + path + " - " + ex.Message);
            }
        }


        // 사용자 인증 함수
        private bool AuthenticateUser(string userName, string password)
        {
            // 레지스트리에서 저장된 ID와 PW를 가져옴
            RegistryKey ftpKey = Registry.CurrentUser.OpenSubKey(@"SoftWare\FTP");
            if (ftpKey != null)
            {
                string storedUserName = (string)ftpKey.GetValue("FTP_ID");
                string storedPassword = (string)ftpKey.GetValue("FTP_PW");

                return storedUserName == userName && storedPassword == password;
            }
            return false;
        }

        // client 접속 해제 
        private void h_client_OnDisconnected(TcpClient clientSocket)
        {
            if (clientList.ContainsKey(clientSocket))
            {
                clientList.Remove(clientSocket);
            }
        }

        // client로 부터 받은 메세지
        private void OnReceived(string message, string userName)
        {
            if (message.Equals("LeaveChat"))
            {
                string displayMessage = "Leave user : " + userName;

                DisplayText(displayMessage);
                SendMessageAll("LeaveChat", userName, true);
                SetUserList(userName, "D");
            }
            else
            {
                string displayMessage = "From Client : " + userName + " : " + message;
                DisplayText(displayMessage); // Server단에 출력
                SendMessageAll(message, userName, true); // 모든 Client에게 전송
            }
        }

        // 메세지 전송
        public void SendMessageAll(string message, string userName, bool flag)
        {
            foreach (var pair in clientList)
            {
                date = DateTime.Now.ToString("yyyy.MM.dd. HH:mm:ss"); // 현재 날짜 받기

                TcpClient client = pair.Key as TcpClient;
                NetworkStream stream = client.GetStream();
                byte[] buffer = null;

                if (flag)
                {
                    if (message.Equals("LeaveChat"))
                        buffer = Encoding.Unicode.GetBytes(userName + " 님이 대화방을 나갔습니다.");
                    else
                        buffer = Encoding.Unicode.GetBytes("[ " + date + " ] " + userName + " : " + message);
                }
                else
                {
                    DisplayText("[ " + date + " ]" + message);
                    buffer = Encoding.Unicode.GetBytes(message);
                }
                stream.Write(buffer, 0, buffer.Length); // 버퍼 쓰기
                stream.Flush();
            }
        }

        // 화면에 text 출력
        private void DisplayText(string text)
        {
            richTextBox1.Invoke((MethodInvoker)delegate { richTextBox1.AppendText(text + "\r\n"); }); // 데이터를 수신창에 표시, 데이터 충돌을 피하기 위해 Invoke 사용
            richTextBox1.Invoke((MethodInvoker)delegate { richTextBox1.ScrollToCaret(); });  // 스크롤을 제일 아래로 설정
        }

        // 접속 client 출력
        private void SetUserList(string userName, string div)
        {
            try
            {
                if (div.Equals("I"))
                {
                    if (listBox1.InvokeRequired)
                    {
                        listBox1.Invoke((MethodInvoker)delegate { listBox1.Items.Add(userName); });
                    }
                    else
                    {
                        listBox1.Items.Add(userName);
                    }
                }
                else if (div.Equals("D"))
                    if (listBox1.InvokeRequired)
                    {
                        listBox1.Invoke((MethodInvoker)delegate { listBox1.Items.Add(userName); });
                    }
                    else
                    {
                        listBox1.Items.Add(userName);
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // FTP 키 생성
        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(createIdBox.Text))
            {
                MessageBox.Show("ID를 입력해주세요.");
                createIdBox.Focus();
                return;
            }else if (string.IsNullOrEmpty(createPwBox.Text))
            {
                MessageBox.Show("PW를 입력해주세요.");
                createPwBox.Focus();
                return;
            }
            else if (Registry.CurrentUser.OpenSubKey(@"SoftWare\FTP") != null)
            {
                MessageBox.Show("FTP 키가 이미 존재합니다.");
                return;
            }
            RegistryKey ftpValue;
            ftpValue = Registry.CurrentUser.CreateSubKey("SoftWare").CreateSubKey("FTP");
            ftpValue.SetValue("FTP_ID", this.createIdBox.Text);
            ftpValue.SetValue("FTP_PW", this.createPwBox.Text);
            MessageBox.Show("FTP 키가 발급되었습니다.");
        }

        // FTP 키 삭제
        private void button2_Click(object sender, EventArgs e)
        {
            Registry.CurrentUser.CreateSubKey("Software").DeleteSubKey("FTP");
            MessageBox.Show("FTP 키가 삭제되었습니다.");
            createIdBox.Clear();
            createPwBox.Clear();
        }
    }
}
