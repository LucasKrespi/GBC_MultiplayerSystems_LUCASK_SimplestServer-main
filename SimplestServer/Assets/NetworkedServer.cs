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

public class ReplayMove
{
    public string m_sMove;
    public int m_iIterator;

    public ReplayMove(string move, int iterator)
    {
        m_sMove = move;
        m_iIterator = iterator;
    }
}
public class Replay
{
    public string replayID;
    public LinkedList<ReplayMove> m_lkMoves;

    public Replay()
    {
        m_lkMoves = new LinkedList<ReplayMove>();
        replayID = ReplayCounter.id.ToString();
        ReplayCounter.id++;
    }
}

public static class ReplayCounter
{
    public static int id = 0;
}


public class GameSession
{
    public int playerID1, playerID2;

    public LinkedList<int> observersID;

    public Replay gsReplay;


    public bool hasStarted = false;



    public GameSession(int PlayerID1, int PlayerID2)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;

        gsReplay = new Replay();
        observersID = new LinkedList<int>();
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
    LinkedList<GameSession> m_ListGameSessions;
    LinkedList<Replay> m_ListOfReplays;

    string m_sPlayerAccDataFilePath;
    string m_sGameSessionReplayDataFilePath;

    int playerWaitingForMatch = -1;

    enum ClientToServerSignifiers
    {
        LOGIN = 0,
        CREATE_USER = 1,
        ADD_TO_GAME_SESSION = 2,
        PLAY_WAS_MADE = 3,
        CHAT_MSG = 4,
        JOIN_AS_OBSERVER = 5,
        LEAVE_GAME_SESSION = 6
    }

    enum ServerToClientSignifiers
    {
        LOGIN_FALIED = -1,
        LOGIN_SUCCESS = 0,
        CREATE_USER_SUCCESS = 1,
        CREATE_USER_FALIED = 2,
        GAME_SESSION_STARTED = 3,
        OPPONENT_PLAY = 4,
        FIRST_PLAYER = 5,
        SECOND_PLAYER = 6,
        CHAT_MSG = 7,
        OBSERVER = 8,
    }
    // Start is called before the first frame update
    void Start()
    {
        m_sPlayerAccDataFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccData.txt";
        m_sGameSessionReplayDataFilePath = Application.dataPath + Path.DirectorySeparatorChar + "Replays.txt";

        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        m_ListPLayerAcc = new LinkedList<PlayerAcc>();
        m_ListGameSessions = new LinkedList<GameSession>();
        m_ListOfReplays = new LinkedList<Replay>();

        LoadPlayerAcc();
        LoadReplay();
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
                        SendMessageToClient(((int)ServerToClientSignifiers.LOGIN_SUCCESS).ToString(), id);
                    }
                    else
                    {
                        SendMessageToClient((int)ServerToClientSignifiers.LOGIN_FALIED + ", Wrong Password ", id);
                    }

                    hasBeenFound = true;
                    break;
                }
            }

            if (!hasBeenFound)
            {
                SendMessageToClient((int)ServerToClientSignifiers.LOGIN_FALIED + ", Client not found ", id);
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
        else if (signifier == (int)ClientToServerSignifiers.ADD_TO_GAME_SESSION)
        {
            if(playerWaitingForMatch == -1)
            {
                playerWaitingForMatch = id;
            }
            else
            {
                GameSession gs = new GameSession(playerWaitingForMatch, id);

                gs.hasStarted = true;

                m_ListGameSessions.AddLast(gs);

                SendMessageToClient(((int)ServerToClientSignifiers.GAME_SESSION_STARTED).ToString()+ "," + (int)ServerToClientSignifiers.SECOND_PLAYER, id);
                SendMessageToClient(((int)ServerToClientSignifiers.GAME_SESSION_STARTED).ToString() + "," + (int)ServerToClientSignifiers.FIRST_PLAYER, playerWaitingForMatch);

                playerWaitingForMatch = -1;

                if (gs.observersID.Count > 0)
                {
                    Debug.Log("ADD TO GAME SESSION LOOP " + gs.observersID.Count);
                    foreach (int oID in gs.observersID)
                    {
                        SendMessageToClient(((int)ServerToClientSignifiers.GAME_SESSION_STARTED).ToString() + "," + (int)ServerToClientSignifiers.OBSERVER, oID);
                    }
                }
              

            }

            
        }
        else if (signifier == (int)ClientToServerSignifiers.PLAY_WAS_MADE)
        {

            GameSession gs = FindGameSessionWithPlayerID(id);

            if(gs.playerID1 == id)
            {
                SendMessageToClient(((int)ServerToClientSignifiers.OPPONENT_PLAY).ToString() + "," + csv[1], gs.playerID2);

                ReplayMove rm = new ReplayMove("X", int.Parse(csv[1]));
                gs.gsReplay.m_lkMoves.AddLast(rm);
                
            }
            else
            {
                SendMessageToClient(((int)ServerToClientSignifiers.OPPONENT_PLAY).ToString() + "," + csv[1], gs.playerID1);

                ReplayMove rm = new ReplayMove("O", int.Parse(csv[1]));
                gs.gsReplay.m_lkMoves.AddLast(rm);
            }

            if (gs.observersID.Count > 0)
            {
                foreach (int oID in gs.observersID)
                {
                    SendMessageToClient(((int)ServerToClientSignifiers.OPPONENT_PLAY).ToString() + "," + csv[1], oID);
                }
            }
        }
        else if (signifier == (int)ClientToServerSignifiers.CHAT_MSG)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);

            List<string> temp = new List<string>(csv);
            temp.RemoveAt(0);

            if (gs.playerID1 == id)
            {
                SendMessageToClient(((int)ServerToClientSignifiers.CHAT_MSG).ToString() + "," + string.Join(", ",temp) , gs.playerID2);
            }
            else
            {
                SendMessageToClient(((int)ServerToClientSignifiers.CHAT_MSG).ToString() + "," + string.Join(", ", temp), gs.playerID1);
            }

            foreach (int oID in gs.observersID)
            {
                SendMessageToClient(((int)ServerToClientSignifiers.CHAT_MSG).ToString() + "," + string.Join(", ", temp), oID);
            }


        }
        else if(signifier == (int)ClientToServerSignifiers.JOIN_AS_OBSERVER)
        {
            //For now can Only join in ther first game session (future search game settion with playerAcc
            GameSession gs = m_ListGameSessions.First.Value;

            if (gs.hasStarted)
            {
                SendMessageToClient(((int)ServerToClientSignifiers.GAME_SESSION_STARTED).ToString() + "," + (int)ServerToClientSignifiers.OBSERVER, id);
            }            
                        
            gs.observersID.AddLast(id);
        }
        else if(signifier == (int)ClientToServerSignifiers.LEAVE_GAME_SESSION)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);
            if (gs != null)
            {
                SaveReplay(gs.gsReplay);
                m_ListGameSessions.Remove(gs);
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

            sr.Close();
        }
    }
   
    private void SaveReplay(Replay replay)
    {
        m_ListOfReplays.AddLast(replay);

        StreamWriter sw = new StreamWriter(m_sGameSessionReplayDataFilePath);

        
        foreach (Replay r in m_ListOfReplays)
        {
            foreach (ReplayMove rm in r.m_lkMoves)
            {
                sw.WriteLine(replay.replayID + "," + rm.m_iIterator + "," + rm.m_sMove);
            }
        }
        sw.Close();
    }


    private void LoadReplay()
    {
        if (File.Exists(m_sGameSessionReplayDataFilePath))
        {
            StreamReader sr = new StreamReader(m_sGameSessionReplayDataFilePath);

            string line;

            Replay r = new Replay();

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                if(r.replayID == csv[0])
                {
                    ReplayMove rm = new ReplayMove(csv[2], int.Parse(csv[1]));

                    r.m_lkMoves.AddLast(rm);
                }
                else
                {
                    m_ListOfReplays.AddLast(r);

                    r = new Replay();

                    ReplayMove rm = new ReplayMove(csv[2], int.Parse(csv[1]));

                    r.m_lkMoves.AddLast(rm);
                }
            }

            m_ListOfReplays.AddLast(r);

            sr.Close();
        }

    }
    private GameSession FindGameSessionWithPlayerID(int ID)
    {
        foreach(GameSession gs in m_ListGameSessions)
        {
            if(gs.playerID1 == ID || gs.playerID2 == ID)
            {
                return gs;
            }
        }

        return null;
    }

}
