using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Serialization;
using BepInEx.Configuration;

namespace Finder;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
internal class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "top.coldwinds.finder";
    public const string PluginName = "Finder";
    public const string PluginVersion = "1.1";

    public static Plugin Instance = null!;
    public static ManualLogSource Log = null!;
    private static readonly Harmony Harmony = new(PluginGuid);

    public static string PluginPath => Path.GetDirectoryName(Instance.Info.Location);

    private bool _showGUI;
    private GameManager _gm;
    private InGameCardBase[] _allCards;
    
    private int _currentPage;
    private int _maxPages;
    private string _searchedCardString;
    private Vector2 _cardsListScrollView;
    private string _pathUrl;
    private ConfigEntry<KeyCode> _hotkey;
    [FormerlySerializedAs("ManualSaveUIPanel0bj1")] public GameObject manualSaveUIPanel0Bj1;
    [FormerlySerializedAs("ManualSaveUIPanel1")] public RectTransform manualSaveUIPanel1;
    [FormerlySerializedAs("myLoadABPrefab")] public AssetBundle myLoadAbPrefab;

    private void Start()
    {
	    _hotkey = Config.Bind<KeyCode>("config", "hotkey", KeyCode.F7, "快捷键");
    }

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        Harmony.PatchAll();
        
        _pathUrl = PluginPath + "\\finder";
        manualSaveUIPanel0Bj1 = new GameObject();
        manualSaveUIPanel1 = new RectTransform();
        myLoadAbPrefab = AssetBundle.LoadFromFile(_pathUrl);
        manualSaveUIPanel0Bj1 = LoadAsset("freetrade");
        DontDestroyOnLoad(manualSaveUIPanel0Bj1);
        manualSaveUIPanel0Bj1.SetActive(false);
        
        Log.LogInfo($"Plugin {PluginName} is loaded!");
    }

    private void Update()
    {
        if (!_gm)
        {
            _gm = MBSingleton<GameManager>.Instance;
        }

        if (Input.GetKeyDown(_hotkey.Value))
        {
            UpdateCard();
            _showGUI = !_showGUI;
        }
    }

    private void OnGUI()
    {
	    if (!_showGUI)
	    {
		    manualSaveUIPanel0Bj1.SetActive(false);
	    }
	    else
	    {
		    manualSaveUIPanel0Bj1.SetActive(true);
		    GUILayout.BeginArea(new Rect(Screen.width * 0.75f, 0f, Screen.width * 0.25f, Screen.height));
		    CardsGUI();
		    GUILayout.EndArea();
	    }
    }

    private void CardsGUI()
    {
	    if (_allCards == null || _allCards.Length == 0)
	    {
		    return;
	    }

	    GUILayout.BeginVertical("box", Array.Empty<GUILayoutOption>());
	    GUILayout.Label("Cards", Array.Empty<GUILayoutOption>());
	    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
	    GUILayout.Label("Search", Array.Empty<GUILayoutOption>());
	    _searchedCardString = GUILayout.TextField(_searchedCardString, Array.Empty<GUILayoutOption>());
	    GUILayout.EndHorizontal();
	    _cardsListScrollView = GUILayout.BeginScrollView(_cardsListScrollView, new GUILayoutOption[]
	    {
		    GUILayout.ExpandHeight(true)
	    });
	    _searchedCardString ??= "";

	    for (int i = 0; i < _allCards.Length; i++)
	    {
		    if (_allCards[i].name.ToLower().Contains(_searchedCardString.ToLower()) || _allCards[i].CardModel.CardName.ToString().ToLower().Contains(_searchedCardString.ToLower()))
		    {
			    if (i / 150 != _currentPage && string.IsNullOrEmpty(_searchedCardString))
			    {
				    if (i >= 150 * _currentPage)
				    {
					    break;
				    }
			    }
			    else
			    {
				    GUILayout.BeginHorizontal("box", Array.Empty<GUILayoutOption>());
				    GUILayout.Label(string.Format("{0} ({1})", _allCards[i].CardModel.CardName, _allCards[i].name), Array.Empty<GUILayoutOption>());
				    GUILayout.FlexibleSpace();
				    if (GUILayout.Button("定位", Array.Empty<GUILayoutOption>()))
				    {
					    GraphicsManager.Instance.MoveViewToSlot(_allCards[i].CurrentSlot, true, true);
				    }

				    GUILayout.EndHorizontal();
			    }
		    }
	    }

	    GUILayout.EndScrollView();
	    if (string.IsNullOrEmpty(_searchedCardString))
	    {
		    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		    if (_currentPage == 0)
		    {
			    GUILayout.Box("<", new GUILayoutOption[]
			    {
				    GUILayout.Width(25f)
			    });
		    }
		    else if (GUILayout.Button("<", new GUILayoutOption[]
		             {
			             GUILayout.Width(25f)
		             }))
		    {
			    _currentPage--;
		    }

		    GUILayout.FlexibleSpace();
		    GUILayout.Label(string.Format("{0}/{1}", (_currentPage + 1).ToString(), _maxPages.ToString()),
			    Array.Empty<GUILayoutOption>());
		    GUILayout.FlexibleSpace();
		    if (_currentPage == _maxPages - 1)
		    {
			    GUILayout.Box(">", new GUILayoutOption[]
			    {
				    GUILayout.Width(25f)
			    });
		    }
		    else if (GUILayout.Button(">", new GUILayoutOption[]
		             {
			             GUILayout.Width(25f)
		             }))
		    {
			    _currentPage++;
		    }

		    GUILayout.EndHorizontal();
	    }

	    GUILayout.EndVertical();
    }

    private void UpdateCard()
    {
	    List<InGameCardBase> list = new List<InGameCardBase>();

	    //加载base类卡牌
        foreach (var card in _gm.BaseCards)
        {
            if (card.CurrentSlotInfo.SlotType != SlotsTypes.Exploration)
            {
                if (!list.Contains(card))
                {
	                list.Add(card);
                }
            }
        }
    
        //加载location、blueprint类卡牌
        foreach (var card in _gm.LocationCards)
        {
            if (card.CardModel.CardType == CardTypes.Location || card.CardModel.CardType == CardTypes.Blueprint &&
                _gm.GameGraphics.BlueprintInstanceGoToLocations)
            {
                if (!list.Contains(card))
                {
	                list.Add(card);
                }
            }
        }
        
        //加载Item类卡牌
        foreach (var card in _gm.ItemCards)
        {
            if (card.CardModel.CardType == CardTypes.Item && card.CurrentSlotInfo.SlotType != SlotsTypes.Exploration)
            {
                if (!list.Contains(card))
                {
	                list.Add(card);
                }
            }
        }
        
        //加载Hand类卡牌
        if (_gm.CurrentHandCard)
        {
            if (!list.Contains(_gm.CurrentHandCard))
            {
	            list.Add(_gm.CurrentHandCard);
            }
        }
        
        //加载liquid类卡牌
        foreach (var card in _gm.LiquidCards)
        {
            if (!list.Contains(card))
            {
	            list.Add(card);
            }
        }
        
        _allCards = list.ToArray();
        _maxPages = list.Count / 150;
    }
    
    public GameObject LoadAsset(string prefabName)
    {
	    return Instantiate(myLoadAbPrefab.LoadAsset<GameObject>(prefabName));
    }
}