using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class PlayerAcc
{
    public string m_name, m_passaword;

    public PlayerAcc(string name, string password)
    {
        this.m_name = name;
        this.m_passaword = password;
    }
}
public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAcc> m_ListPLayerAcc;

    string m_sPlayerAccDataFilePath;

    enum ClientToServerSignifiers
    {
        LOGIN = 0,
        CREATE_USER = 1
    }

    enum ServerToClientSignifiers
    {
        LOGIN_FALIED = -1,
        LOGIN_SUCCESS = 0,
        CREATE_USER_SUCCESS = 1,
        CREATE_USER_FALIED = 2
    }
    // Start is called before the first frame update
    void Start()
    {
        m_sPlayerAccDataFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccData.txt";


        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        m_ListPLayerAcc = new LinkedList<PlayerAcc>();

       

        LoadPlayerAcc();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        

        if (signifier == (int)ClientToServerSignifiers.LOGIN)
        {
            string n = csv[1];

            string p = csv[2];

            bool hasBeenFound = false;
           

            foreach(PlayerAcc pa in m_ListPLayerAcc)
            {
                if(pa.m_name == n)
                {
                    if(pa.m_passaword == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LOGIN_SUCCESS.ToString(), id);
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LOGIN_FALIED.ToString() + " Wrong Password ", id);
                    }

                    hasBeenFound = true;
                    break;
                }
            }

            if (!hasBeenFound)
            {
                SendMessageToClient(ServerToClientSignifiers.LOGIN_FALIED.ToString() + " Client not found ", id);
            }

        }
        else if(signifier == (int)ClientToServerSignifiers.CREATE_USER)
        {
            string n = csv[1];

            string p = csv[2];

            bool isUnique = true;

            foreach (PlayerAcc pa in m_ListPLayerAcc)
            {
              
                if (pa.m_name == n)
                {
                    Debug.Log(pa.m_name);
                    isUnique = false;
                    break;
                }

               
            }

            if (isUnique)
            {

                m_ListPLayerAcc.AddLast(new PlayerAcc(n, p));

                SendMessageToClient((int)ServerToClientSignifiers.CREATE_USER_SUCCESS + "," + ServerToClientSignifiers.CREATE_USER_SUCCESS, id);

                SavePlayerAcc();
            }
            else
            {
  
                SendMessageToClient((int)ServerToClientSignifiers.CREATE_USER_FALIED + "," + ServerToClientSignifiers.CREATE_USER_FALIED, id);
            }
        }
    }


    private void SavePlayerAcc()
    {
        StreamWriter sw = new StreamWriter(m_sPlayerAccDataFilePath);
        
        foreach(PlayerAcc pa in m_ListPLayerAcc)
        {
            sw.WriteLine(pa.m_name + "," + pa.m_passaword);
        }

        sw.Close();
    }

    private void LoadPlayerAcc()
    {
        if (File.Exists(m_sPlayerAccDataFilePath))
        {
            StreamReader sr = new StreamReader(m_sPlayerAccDataFilePath);

            string line;

            while((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                PlayerAcc pa = new PlayerAcc(csv[0], csv[1]);

                m_ListPLayerAcc.AddLast(pa);
            }

        }
    }
}
