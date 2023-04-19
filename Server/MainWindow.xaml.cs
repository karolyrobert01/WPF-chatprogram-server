using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    enum Command
    {
        Login,      //Log into the server
        Logout,     //Logout of the server
        Message,    //Send a text message to all the chat clients
        List,       //Get a list of users in the chat room from the server
        Register,
        Accept,
        Decline,
        Null        //No command
    }

    public partial class MainWindow : Window
    {
        
        //readonly string XMLfile = "C:\\Users\\Károly Róbert\\Desktop\\Tavkozlo\\Final\\Vegleges\\2 esre megvan\\Projekt\\Server\\Server\\XML\\XMLFile1.xml";
        readonly string XMLfile = "C:\\Users\\Károly Róbert\\Desktop\\tavk_main\\Server\\Server\\XML\\XMLFile1.xml";


        struct ClientInfo
        {
            public Socket socket;   //Socket of the client
            public string strName;  //Name by which the user logged into the chat room
            public string strPass;
        }

        ArrayList clientList;

        Socket serverSocket;

        byte[] byteData = new byte[1024];

        
        public MainWindow()
        {
            clientList = new ArrayList();
            InitializeComponent();            

        }

        private delegate void UpdateDelegate(string pMessage);

        private void UpdateMessage(string pMessage)
        {
            this.textBox1.Text += pMessage;
        }      

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //We are using TCP sockets
                //Control.CheckForIllegalCrossThreadCalls = false;
                serverSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);

                //Assign the any IP of the machine and listen on port number 1000
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1000);

                //Bind and listen on the given address
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(4);

                //Accept the incoming clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
                //serverSocket.Accept();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSserverTCP");
            }   
        }
        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = serverSocket.EndAccept(ar);

                //Start listening for more clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands from her
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSserverTCP");
            }
        }

        

        private void OnReceive(IAsyncResult ar)
        {
            Socket clientSocket = null;
            try
            {
                clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndReceive(ar);

                //Transform the array of bytes received from the user into an
                //intelligent form of object Data
                Data msgReceived = new Data(byteData);

                //We will send this object in response the users request
                Data msgToSend = new Data();

                byte[] message;

                //If the message is to login, logout, or simple text message
                //then when send to others the type of the message remains the same
                msgToSend.cmdCommand = msgReceived.cmdCommand;
                msgToSend.strName = msgReceived.strName;
                msgToSend.strPass = msgReceived.strPass;
                msgToSend.strAddres = msgReceived.strAddres;

                switch (msgReceived.cmdCommand)
                {
                    case Command.Login:
                        string regName = "";
                        string regPass = "";
                        bool find = false;
                        // Start with XmlReader object  
                        //here, we try to setup Stream between the XML file nad xmlReader  
                        using (XmlReader reader = XmlReader.Create(XMLfile))
                        {
                            while (reader.Read() && !find)
                            {
                                if (reader.IsStartElement())
                                {
                                    //return only when you have START tag  
                                    switch (reader.Name.ToString())
                                    {
                                        case "Name":
                                            //Console.WriteLine("Name of the Element is : " + reader.ReadString());
                                            regName = reader.ReadString();
                                            break;
                                        case "Password":
                                            //Console.WriteLine("Your Location is : " + reader.ReadString());
                                            regPass = reader.ReadString();
                                            break;
                                    }
                                    if(regName == msgReceived.strName && regPass == msgReceived.strPass)
                                    {
                                        find = true;
                                        break;
                                    }
                                }
                                
                            }
                        }
                        if (find)
                        {
                            //When a user logs in to the server then we add her to our
                            //list of clients

                            msgToSend.cmdCommand = Command.Accept;
                            //message = msgToSend.ToByte();
                            //clientSocket.Send(message);


                            ClientInfo clientInfo = new ClientInfo();
                            clientInfo.socket = clientSocket;
                            clientInfo.strName = msgReceived.strName;
                            clientInfo.strPass = msgReceived.strPass;

                            clientList.Add(clientInfo);

                            //Set the text of the message that we will broadcast to all users
                            msgToSend.strMessage = "<<<" + msgReceived.strName + " has joined the room>>>";
                        }
                        else
                        {
                            msgToSend.cmdCommand = Command.Decline;
                        }
                        
                        break;

                    case Command.Logout:

                        //When a user wants to log out of the server then we search for her 
                        //in the list of clients and close the corresponding connection

                        int nIndex = 0;
                        foreach (ClientInfo client in clientList)
                        {
                            if (client.socket == clientSocket)
                            {
                                clientList.RemoveAt(nIndex);
                                break;
                            }
                            ++nIndex;
                        }
                        
                        clientSocket.Close();
                        msgToSend.strMessage = "<<<" +msgReceived.strName + " has left the room>>>";
                        break;

                    case Command.Message:

                        //Set the text of the message that we will broadcast to all users 
                       // msgToSend.strMessage = msgReceived.strName +":"+msgReceived.strAddres +": " + msgReceived.strMessage;
                        
                        msgToSend.cmdCommand = Command.Message;
                        msgToSend.strMessage = null;
                        msgToSend.strName = msgReceived.strName;
                        msgToSend.strPass = msgReceived.strPass;
                        msgToSend.strAddres =msgReceived.strAddres;
                        bool toAllUser = true;
                        if (msgReceived.strAddres != msgReceived.strName)
                        {
                            //Collect the names of the user in the chat room
                            foreach (ClientInfo client in clientList)
                            {

                                if (client.strName == msgReceived.strAddres)
                                {
                                    msgToSend.strMessage = "*" + msgReceived.strName + ": " + msgReceived.strMessage;
                                    message = msgToSend.ToByte();
                                    client.socket.Send(message, 0, message.Length, SocketFlags.None);
                                    toAllUser = false;
                                }
                                if (client.strName == msgReceived.strName)
                                {
                                    msgToSend.strMessage = "Me: " + msgReceived.strMessage;
                                    message = msgToSend.ToByte();
                                    client.socket.Send(message, 0, message.Length, SocketFlags.None);
                                }
                            }

                            if (toAllUser)
                            {
                                foreach (ClientInfo client in clientList)
                                {
                                    if (client.strName != msgReceived.strName)
                                    {
                                        msgToSend.strMessage = msgReceived.strName + ": " + msgReceived.strMessage;
                                        message = msgToSend.ToByte();
                                        client.socket.Send(message, 0, message.Length, SocketFlags.None);
                                    }

                                }
                            }

                            if (toAllUser == true)
                            {
                                msgToSend.strMessage = msgReceived.strName + " -> All: " + msgReceived.strMessage;
                            }
                            else
                            {
                                msgToSend.strMessage = msgReceived.strName + " -> " + msgReceived.strAddres + ": " + msgReceived.strMessage;
                            }
                        }
                        if (msgToSend.strMessage != null)
                        {
                            UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                            this.textBox1.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, update,
                            msgToSend.strMessage + "\r\n");
                        }
                        break;

                    case Command.List:

                        //Send the names of all users in the chat room to the new user
                        msgToSend.cmdCommand = Command.List;
                        msgToSend.strName = null;
                        msgToSend.strMessage = null;
                        msgToSend.strPass = null;
                        msgToSend.strAddres = null;


                        msgToSend.strMessage += "Résztvevők: ";
                        //Collect the names of the user in the chat room
                        foreach (ClientInfo client in clientList)
                        {
                            //To keep things simple we use asterisk as the marker to separate the user names
                            msgToSend.strMessage += client.strName + " ; ";
                        }
                        

                        message = msgToSend.ToByte();

                        //Send the name of the users in the chat room
                        clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSend), clientSocket);
                        break;

                    case Command.Register:
                        string registerName = "papu";
                        bool findReg = false;
                        // Start with XmlReader object  
                        //here, we try to setup Stream between the XML file nad xmlReader  
                        using (XmlReader reader = XmlReader.Create(XMLfile))
                        {
                            while (reader.Read() && !findReg)
                            {
                                if (reader.IsStartElement())
                                {
                                    //return only when you have START tag  
                                    switch (reader.Name.ToString())
                                    {
                                        case "Name":
                                            //Console.WriteLine("Name of the Element is : " + reader.ReadString());
                                            registerName = reader.ReadString();
                                            break;
                                        case "Password":
                                            //Console.WriteLine("Your Location is : " + reader.ReadString());
                                            
                                            break;
                                    }
                                    if (registerName == msgReceived.strName)
                                    {
                                        findReg = true;
                                        break;
                                    }
                                }

                            }
                        }

                        if (!findReg)
                        {
                            msgToSend.cmdCommand = Command.Accept; 
                            XDocument doc = XDocument.Load(XMLfile);
                            doc.Root.Add(new XElement("User",
                                new XElement("Name", msgReceived.strName),
                                new XElement("Password", msgReceived.strPass)
                                ));
                            doc.Save(XMLfile);
                        }
                        else
                        {
                            msgToSend.cmdCommand = Command.Decline;
                            clientSocket.Close();
                        }
                        break;

                }

                if (msgToSend.cmdCommand != Command.List && msgToSend.cmdCommand != Command.Message)   //List messages are not broadcasted
                {

            
                    message = msgToSend.ToByte();
                    if (msgToSend.cmdCommand == Command.Accept || msgToSend.cmdCommand == Command.Decline)
                    {
                        clientSocket.Send(message, 0, message.Length, SocketFlags.None);
                    }
                    else
                    {
                        foreach (ClientInfo clientInfo in clientList)
                        {
                            if (clientInfo.socket != clientSocket ||
                            msgToSend.cmdCommand != Command.Login)
                            {
                                //Send the message to all users
                                //clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                //new AsyncCallback(OnSend), clientInfo.socket);
                                clientInfo.socket.Send(message, 0, message.Length, SocketFlags.None);

                            }
                        }
                    }
                    //textBox1.Text += msgToSend.strMessage;
                    if (msgToSend.strMessage != null)
                    {
                        UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                        this.textBox1.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, update,
                            msgToSend.strMessage + "\r\n");
                    }

                }

                //If the user is logging out then we need not listen from her
                if (msgReceived.cmdCommand != Command.Logout && msgReceived.cmdCommand != Command.Decline)
                {
                    //Start listening to the message send by the user
                    clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "SGSserverTCP2");
                clientSocket.Close();
             
            }
        }

        public void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSserverTCP");
            }
        }
    }

    class Data
    {
        //Default constructor
        public Data()
        {
            this.cmdCommand = Command.Null;
            this.strMessage = null;
            this.strName = null;
            this.strPass = null;
            this.strAddres = null;
        }

        //Converts the bytes into an object of type Data
        public Data(byte[] data)
        {
            //The first four bytes are for the Command
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);

            //The next four store the length of the name
            int nameLen = BitConverter.ToInt32(data, 4);

            //The next four store the length of the message
            int msgLen = BitConverter.ToInt32(data, 8);

            int passLen = BitConverter.ToInt32(data, 12);

            int addresLen = BitConverter.ToInt32(data, 16);

            //This check makes sure that strName has been passed in the array of bytes
            if (nameLen > 0)
                this.strName = Encoding.UTF8.GetString(data, 20, nameLen);
            else
                this.strName = null;

            //This checks for a null message field
            if (msgLen > 0)
                this.strMessage = Encoding.UTF8.GetString(data, 20 + nameLen, msgLen);
            else
                this.strMessage = null;

            if (passLen > 0)
                this.strPass = Encoding.UTF8.GetString(data, 20 + nameLen + msgLen, passLen);
            else
                this.strPass = null;

            if (addresLen > 0)
                this.strAddres = Encoding.UTF8.GetString(data, 20 + nameLen + msgLen +passLen, addresLen);
            else
                this.strAddres = null;
        }

        //Converts the Data structure into an array of bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>();

            //First four are for the Command
            result.AddRange(BitConverter.GetBytes((int)cmdCommand));

            //Add the length of the name
            if (strName != null)
                result.AddRange(BitConverter.GetBytes(Encoding.UTF8.GetByteCount(strName)));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Length of the message
            if (strMessage != null)
                result.AddRange(BitConverter.GetBytes(Encoding.UTF8.GetByteCount(strMessage)));
            else
                result.AddRange(BitConverter.GetBytes(0));

            if (strPass != null)
                result.AddRange(BitConverter.GetBytes(Encoding.UTF8.GetByteCount(strPass)));
            else
                result.AddRange(BitConverter.GetBytes(0));

            if (strAddres != null)
                result.AddRange(BitConverter.GetBytes(Encoding.UTF8.GetByteCount(strAddres)));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Add the name
            if (strName != null)
                result.AddRange(Encoding.UTF8.GetBytes(strName));

            //And, lastly we add the message text to our array of bytes
            if (strMessage != null)
                result.AddRange(Encoding.UTF8.GetBytes(strMessage));

            if (strPass != null)
                result.AddRange(Encoding.UTF8.GetBytes(strPass));

            if (strAddres != null)
                result.AddRange(Encoding.UTF8.GetBytes(strAddres));

            return result.ToArray();
        }

        public string strName;      //Name by which the client logs into the room
        public string strMessage;   //Message text
        public string strPass;
        public string strAddres;
        public Command cmdCommand;  //Command type (login, logout, send message, etcetera)
    } 
}
