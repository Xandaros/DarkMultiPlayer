using System;
using System.Collections.Generic;
using UnityEngine;
using DarkMultiPlayerCommon;
using System.IO;
using MessageStream;

namespace DarkMultiPlayer
{
    public class CraftLibraryWorker
    {
        //Public
        private Client parent;
        public bool display;
        public bool workerEnabled;
        //Private
        private Queue<CraftAddEntry> craftAddQueue;
        private Queue<CraftDeleteEntry> craftDeleteQueue;
        private Queue<CraftResponseEntry> craftResponseQueue;
        private bool safeDisplay;
        private bool initialized;
        private bool showUpload;
        private string selectedPlayer;
        private List<string> playersWithCrafts;
        //Player -> Craft type -> Craft name
        private Dictionary<string, Dictionary<CraftType, List<string>>> playerList;
        //Craft type -> Craft name
        private Dictionary<CraftType, List<string>> uploadList;
        //GUI Layout
        private Rect playerWindowRect;
        private Rect libraryWindowRect;
        private Rect moveRect;
        private GUILayoutOption[] playerLayoutOptions;
        private GUILayoutOption[] libraryLayoutOptions;
        private GUILayoutOption[] textAreaOptions;
        private GUIStyle windowStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle scrollStyle;
        private Vector2 playerScrollPos;
        private Vector2 libraryScrollPos;
        //save paths
        private string savePath;
        private string vabPath;
        private string sphPath;
        private string subassemblyPath;
        //upload event
        private CraftType uploadCraftType;
        private string uploadCraftName;
        //download event
        private CraftType downloadCraftType;
        private string downloadCraftName;
        //delete event
        private CraftType deleteCraftType;
        private string deleteCraftName;
        //const
        private const float PLAYER_WINDOW_HEIGHT = 300;
        private const float PLAYER_WINDOW_WIDTH = 200;
        private const float LIBRARY_WINDOW_HEIGHT = 400;
        private const float LIBRARY_WINDOW_WIDTH = 300;

        public CraftLibraryWorker(Client parent)
        {
            this.parent = parent;
            if (this.parent != null)
            {
                return;
            }
        }

        public void Update()
        {
            if (workerEnabled)
            {
                while (craftAddQueue.Count > 0)
                {
                    CraftAddEntry cae = craftAddQueue.Dequeue();
                    AddCraftEntry(cae.playerName, cae.craftType, cae.craftName);
                }
                while (craftDeleteQueue.Count > 0)
                {
                    CraftDeleteEntry cde = craftDeleteQueue.Dequeue();
                    DeleteCraftEntry(cde.playerName, cde.craftType, cde.craftName);
                }
                while (craftResponseQueue.Count > 0)
                {
                    CraftResponseEntry cre = craftResponseQueue.Dequeue();
                    SaveCraftFile(cre.craftType, cre.craftName, cre.craftData);
                }
                safeDisplay = display;
                if (uploadCraftName != null)
                {
                    UploadCraftFile(uploadCraftType, uploadCraftName);
                    uploadCraftName = null;
                    uploadCraftType = CraftType.VAB;
                }
                if (downloadCraftName != null)
                {
                    DownloadCraftFile(selectedPlayer, downloadCraftType, downloadCraftName);
                    downloadCraftName = null;
                    downloadCraftType = CraftType.VAB;
                }
                if (deleteCraftName != null)
                {
                    DeleteCraftEntry(parent.settings.playerName, deleteCraftType, deleteCraftName);
                    using (MessageWriter mw = new MessageWriter())
                    {
                        mw.Write<int>((int)CraftMessageType.DELETE_FILE);
                        mw.Write<string>(parent.settings.playerName);
                        mw.Write<int>((int)deleteCraftType);
                        mw.Write<string>(deleteCraftName);
                        parent.networkWorker.SendCraftLibraryMessage(mw.GetMessageBytes());
                    }
                    deleteCraftName = null;
                    deleteCraftType = CraftType.VAB;
                }
            }
        }

        private void UploadCraftFile(CraftType type, string name)
        {
            string uploadPath = "";
            switch (uploadCraftType)
            {
                case CraftType.VAB:
                    uploadPath = vabPath;
                    break;
                case CraftType.SPH:
                    uploadPath = sphPath;
                    break;
                case CraftType.SUBASSEMBLY:
                    uploadPath = subassemblyPath;
                    break;
            }
            string filePath = Path.Combine(uploadPath, name + ".craft");
            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                using (MessageWriter mw = new MessageWriter())
                {
                    mw.Write<int>((int)CraftMessageType.UPLOAD_FILE);
                    mw.Write<string>(parent.settings.playerName);
                    mw.Write<int>((int)type);
                    mw.Write<string>(name);
                    mw.Write<byte[]>(fileData);
                    parent.networkWorker.SendCraftLibraryMessage(mw.GetMessageBytes());
                    AddCraftEntry(parent.settings.playerName, uploadCraftType, uploadCraftName);

                }
            }
            else
            {
                DarkLog.Debug("Cannot upload file, " + filePath + " does not exist!");
            }

        }

        private void DownloadCraftFile(string playerName, CraftType craftType, string craftName)
        {

            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>((int)CraftMessageType.REQUEST_FILE);
                mw.Write<string>(parent.settings.playerName);
                mw.Write<string>(playerName);
                mw.Write<int>((int)craftType);
                mw.Write<string>(craftName);
                parent.networkWorker.SendCraftLibraryMessage(mw.GetMessageBytes());
            }
        }

        private void AddCraftEntry(string playerName, CraftType craftType, string craftName)
        {
            if (!playersWithCrafts.Contains(playerName))
            {
                playersWithCrafts.Add(playerName);
            }
            if (!playerList.ContainsKey(playerName))
            {
                playerList.Add(playerName, new Dictionary<CraftType, List<string>>());
            }
            if (!playerList[playerName].ContainsKey(craftType))
            {
                playerList[playerName].Add(craftType, new List<string>());
            }
            if (!playerList[playerName][craftType].Contains(craftName))
            {
                DarkLog.Debug("Adding " + craftName + ", type: " + craftType.ToString() + " from " + playerName);
                playerList[playerName][craftType].Add(craftName);
            }
        }

        private void DeleteCraftEntry(string playerName, CraftType craftType, string craftName)
        {
            if (playerList.ContainsKey(playerName))
            {
                if (playerList[playerName].ContainsKey(craftType))
                {
                    if (playerList[playerName][craftType].Contains(craftName))
                    {
                        playerList[playerName][craftType].Remove(craftName);
                        if (playerList[playerName][craftType].Count == 0)
                        {
                            playerList[playerName].Remove(craftType);
                        }
                        if (playerList[playerName].Count == 0)
                        {
                            if (playerName != parent.settings.playerName)
                            {
                                playerList.Remove(playerName);
                                if (playersWithCrafts.Contains(playerName))
                                {
                                    playersWithCrafts.Remove(playerName);
                                }
                            }
                        }
                    }
                    else
                    {
                        DarkLog.Debug("Cannot remove craft entry " + craftName + " for player " + playerName + ", craft does not exist");
                    }
                }
                else
                {
                    DarkLog.Debug("Cannot remove craft entry " + craftName + " for player " + playerName + ", player does not have any " + craftType + " entries");
                }

            }
            else
            {
                DarkLog.Debug("Cannot remove craft entry " + craftName + " for player " + playerName + ", no player entry");
            }
        }

        private void SaveCraftFile(CraftType craftType, string craftName, byte[] craftData)
        {
            string savePath = "";
            switch (uploadCraftType)
            {
                case CraftType.VAB:
                    savePath = vabPath;
                    break;
                case CraftType.SPH:
                    savePath = sphPath;
                    break;
                case CraftType.SUBASSEMBLY:
                    savePath = subassemblyPath;
                    break;
            }
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            string craftFile = Path.Combine(savePath, craftName + ".craft");
            File.WriteAllBytes(craftFile, craftData);
            ScreenMessages.PostScreenMessage("Craft " + craftName + " saved!", 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void InitGUI()
        {
            //Setup GUI stuff
            //left 50, middle height
            playerWindowRect = new Rect(50, (Screen.height / 2f) - (PLAYER_WINDOW_HEIGHT / 2f), PLAYER_WINDOW_WIDTH, PLAYER_WINDOW_HEIGHT);
            //middle of the screen
            libraryWindowRect = new Rect((Screen.width / 2f) - (LIBRARY_WINDOW_WIDTH / 2f), (Screen.height / 2f) - (LIBRARY_WINDOW_HEIGHT / 2f), LIBRARY_WINDOW_WIDTH, LIBRARY_WINDOW_HEIGHT);
            moveRect = new Rect(0, 0, 10000, 20);

            playerLayoutOptions = new GUILayoutOption[4];
            playerLayoutOptions[0] = GUILayout.MinWidth(PLAYER_WINDOW_WIDTH);
            playerLayoutOptions[1] = GUILayout.MaxWidth(PLAYER_WINDOW_WIDTH);
            playerLayoutOptions[2] = GUILayout.MinHeight(PLAYER_WINDOW_HEIGHT);
            playerLayoutOptions[3] = GUILayout.MaxHeight(PLAYER_WINDOW_HEIGHT);

            libraryLayoutOptions = new GUILayoutOption[4];
            libraryLayoutOptions[0] = GUILayout.MinWidth(LIBRARY_WINDOW_WIDTH);
            libraryLayoutOptions[1] = GUILayout.MaxWidth(LIBRARY_WINDOW_WIDTH);
            libraryLayoutOptions[2] = GUILayout.MinHeight(LIBRARY_WINDOW_HEIGHT);
            libraryLayoutOptions[3] = GUILayout.MaxHeight(LIBRARY_WINDOW_HEIGHT);

            windowStyle = new GUIStyle(GUI.skin.window);
            buttonStyle = new GUIStyle(GUI.skin.button);
            labelStyle = new GUIStyle(GUI.skin.label);
            scrollStyle = new GUIStyle(GUI.skin.scrollView);

            textAreaOptions = new GUILayoutOption[2];
            textAreaOptions[0] = GUILayout.ExpandWidth(false);
            textAreaOptions[1] = GUILayout.ExpandWidth(false);
        }

        public void Draw()
        {
            if (!initialized)
            {
                initialized = true;
                InitGUI();
            }
            if (safeDisplay)
            {
                playerWindowRect = GUILayout.Window(GUIUtility.GetControlID(6707, FocusType.Passive), playerWindowRect, DrawPlayerContent, "DarkMultiPlayer - Craft Library", windowStyle, playerLayoutOptions);
            }
            if (safeDisplay && selectedPlayer != null)
            {
                //Sanity check
                if (playersWithCrafts.Contains(selectedPlayer))
                {
                    libraryWindowRect = GUILayout.Window(GUIUtility.GetControlID(6708, FocusType.Passive), libraryWindowRect, DrawLibraryContent, "DarkMultiPlayer - " + selectedPlayer + " Craft Library", windowStyle, libraryLayoutOptions);
                }
                else
                {
                    selectedPlayer = null;
                }
            }
        }

        private void DrawPlayerContent(int windowID)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(moveRect);
            //Draw the player buttons
            playerScrollPos = GUILayout.BeginScrollView(playerScrollPos, scrollStyle);
            foreach (string playerName in playersWithCrafts)
            {
                DrawPlayerButton(playerName);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawPlayerButton(string playerName)
        {
            bool buttonSelected = GUILayout.Toggle(selectedPlayer == playerName, playerName, buttonStyle);
            if (buttonSelected && selectedPlayer != playerName)
            {
                //Select
                selectedPlayer = playerName;
            }
            if (!buttonSelected && selectedPlayer == playerName)
            {
                //Unselect
                selectedPlayer = null;
            }
        }

        private void DrawLibraryContent(int windowID)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(moveRect);
            libraryScrollPos = GUILayout.BeginScrollView(libraryScrollPos, scrollStyle);
            bool newShowUpload = false;
            if (selectedPlayer == parent.settings.playerName)
            {
                newShowUpload = GUILayout.Toggle(showUpload, "Upload", buttonStyle);
            }
            if (newShowUpload && !showUpload)
            {
                //Build list when the upload button is pressed.
                BuildUploadList();
            }
            showUpload = newShowUpload;
            if (showUpload)
            {
                //Draw upload screen
                DrawUploadScreen();
            }
            else
            {
                //Draw download screen
                DrawDownloadScreen();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawUploadScreen()
        {
            foreach (KeyValuePair<CraftType, List<string>> entryType in uploadList)
            {
                GUILayout.Label(entryType.Key.ToString(), labelStyle);
                foreach (string entryName in entryType.Value)
                {
                    if (GUILayout.Button(entryName, buttonStyle))
                    {
                        uploadCraftType = entryType.Key;
                        uploadCraftName = entryName;
                    }
                }
            }
        }

        private void BuildUploadList()
        {
            uploadList = new Dictionary<CraftType, List<string>>();
            if (Directory.Exists(vabPath))
            {
                uploadList.Add(CraftType.VAB, new List<string>());
                string[] craftFiles = Directory.GetFiles(vabPath);
                foreach (string craftFile in craftFiles)
                {
                    string craftName = Path.GetFileNameWithoutExtension(craftFile);
                    uploadList[CraftType.VAB].Add(craftName);
                }
            }
            if (Directory.Exists(sphPath))
            {
                uploadList.Add(CraftType.SPH, new List<string>());
                string[] craftFiles = Directory.GetFiles(sphPath);
                foreach (string craftFile in craftFiles)
                {
                    string craftName = Path.GetFileNameWithoutExtension(craftFile);
                    uploadList[CraftType.SPH].Add(craftName);
                }
            }
            if (Directory.Exists(vabPath))
            {
                uploadList.Add(CraftType.SUBASSEMBLY, new List<string>());
                string[] craftFiles = Directory.GetFiles(subassemblyPath);
                foreach (string craftFile in craftFiles)
                {
                    string craftName = Path.GetFileNameWithoutExtension(craftFile);
                    uploadList[CraftType.SUBASSEMBLY].Add(craftName);
                }
            }
        }

        private void DrawDownloadScreen()
        {
            if (playerList.ContainsKey(selectedPlayer))
            {
                foreach (KeyValuePair<CraftType, List<string>> entry in playerList[selectedPlayer])
                {
                    GUILayout.Label(entry.Key.ToString(), labelStyle);
                    foreach (string craftName in entry.Value)
                    {
                        if (selectedPlayer == parent.settings.playerName)
                        {
                            //Also draw remove button on player screen
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(craftName, buttonStyle))
                            {
                                downloadCraftType = entry.Key;
                                downloadCraftName = craftName;
                            }
                            if (GUILayout.Button("Remove", buttonStyle))
                            {
                                deleteCraftType = entry.Key;
                                deleteCraftName = craftName;
                            }
                            GUILayout.EndHorizontal();
                        }
                        else
                        {
                            if (GUILayout.Button(craftName, buttonStyle))
                            {
                                downloadCraftType = entry.Key;
                                downloadCraftName = craftName;
                            }
                        }
                    }
                }
            }
        }

        public void QueueCraftAdd(CraftAddEntry entry)
        {
            craftAddQueue.Enqueue(entry);
        }

        public void QueueCraftDelete(CraftDeleteEntry entry)
        {
            craftDeleteQueue.Enqueue(entry);
        }

        public void QueueCraftResponse(CraftResponseEntry entry)
        {
            craftResponseQueue.Enqueue(entry);
        }

        public void Reset()
        {
            workerEnabled = false;
            display = false;
            safeDisplay = false;
            showUpload = false;
            selectedPlayer = "";
            workerEnabled = false;
            showUpload = false;
            selectedPlayer = null;
            //Reset state info
            craftAddQueue = new Queue<CraftAddEntry>();
            craftDeleteQueue = new Queue<CraftDeleteEntry>();
            craftResponseQueue = new Queue<CraftResponseEntry>();
            playerList = new Dictionary<string, Dictionary<CraftType, List<string>>>();
            //Make sure we are always at the top so we can upload
            playersWithCrafts = new List<string>();
            playersWithCrafts.Add(parent.settings.playerName);

            playerScrollPos = new Vector2(0, 0);
            libraryScrollPos = new Vector2(0, 0);
            //Set paths
            savePath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "saves"), "DarkMultiPlayer");
            vabPath = Path.Combine(Path.Combine(savePath, "Ships"), "VAB");
            sphPath = Path.Combine(Path.Combine(savePath, "Ships"), "SPH");
            subassemblyPath = Path.Combine(savePath, "Subassemblies");
            BuildUploadList();
            uploadCraftName = null;
        }
    }

    public class CraftAddEntry
    {
        public string playerName;
        public CraftType craftType;
        public string craftName;
    }

    public class CraftDeleteEntry
    {
        public string playerName;
        public CraftType craftType;
        public string craftName;
    }

    public class CraftResponseEntry
    {
        public string playerName;
        public CraftType craftType;
        public string craftName;
        public byte[] craftData;
    }
}

