using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using static WowHeadParser.MainWindow;

namespace WowHeadParser.Entities
{
    class Gameobject : Entity
    {
        struct GameObjectParsing
        {
            public int id;
            public string name;
        }

        public class GameObjectLootParsing
        {
            public int id;
            public int count;
            public dynamic modes;
            public int[] stack;

            public string percent;
            public string questRequired;
        }

        public class GameObjectLootItemParsing : GameObjectLootParsing
        {
            public int classs;
        }

        public class GameObjectLootCurrencyParsing : GameObjectLootParsing
        {
            public int category;
            public string icon;
        }

        public Gameobject()
        {
            m_data.id = 0;
        }

        public Gameobject(int id)
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/object=" + m_data.id;
        }

        public override List<Entity> GetIdsFromZone(String zoneId, String zoneHtml)
        {
            String pattern = @"new Listview\(\{template: 'object', id: 'objects', name: WH.TERMS.objects, tabs: tabsRelated, parent: 'lkljbjkb574',(.*)data: (.+)\}\);";
            String gameobjectJSon = Tools.ExtractJsonFromWithPattern(zoneHtml, pattern, 1);

            List<Entity> tempArray = new List<Entity>();
            if (gameobjectJSon != null)
            {
                List<GameObjectParsing> parsingArray = JsonConvert.DeserializeObject<List<GameObjectParsing>>(gameobjectJSon);
                foreach (GameObjectParsing gameobjectTemplateStruct in parsingArray)
                {
                    Gameobject gameobject = new Gameobject(gameobjectTemplateStruct.id);
                    tempArray.Add(gameobject);
                }
            }


            return tempArray;
        }

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;

            bool optionSelected = false;
            String gameobjectHtml = Tools.GetHtmlFromWowhead(GetWowheadUrl());

            String gameobjectDataPattern = @"\$\.extend\(g_objects\[" + m_data.id + @"\], (.+)\);";

            String gameobjectDataJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectDataPattern);
            if (gameobjectDataJSon != null)
            {
                m_data = JsonConvert.DeserializeObject<GameObjectParsing>(gameobjectDataJSon);
            }

            if (IsCheckboxChecked("locale"))
                optionSelected = true;

            if (IsCheckboxChecked("loot"))
            {
                String gameobjectLootPattern = @"new Listview\(\{template: 'item', id: 'contains', name: WH.TERMS.contains,.*data:(.+)}\);";
                String gameobjectLootCurrencyPattern = @"new Listview\({template: 'currency', id: 'contains-currency', name: WH.TERMS.currencies,.*data:(.+)}\);";

                String gameobjectLootItemJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectLootPattern);
                String gameobjectLootCurrencyJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectLootCurrencyPattern);
                if (gameobjectLootItemJSon != null || gameobjectLootCurrencyJSon != null)
                {
                    GameObjectLootItemParsing[] gameobjectLootItemDatas = gameobjectLootItemJSon != null ? JsonConvert.DeserializeObject<GameObjectLootItemParsing[]>(gameobjectLootItemJSon) : new GameObjectLootItemParsing[0];
                    GameObjectLootCurrencyParsing[] gameobjectLootCurrencyDatas = gameobjectLootCurrencyJSon != null ? JsonConvert.DeserializeObject<GameObjectLootCurrencyParsing[]>(gameobjectLootCurrencyJSon) : new GameObjectLootCurrencyParsing[0];
                    SetGameobjectLootData(gameobjectLootItemDatas, gameobjectLootCurrencyDatas);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("herbalism"))
            {       
                String gameobjectHerboPattern = @"new Listview\(\{template: 'item', id: 'herbalism',.*_totalCount: ([0-9]+),.*data: (.+)\}\);";

                String gameobjectHerbalismTotalCount = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectHerboPattern, 0);
                String gameobjectHerbalismJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectHerboPattern, 1);
                if (gameobjectHerbalismJSon != null)
                {
                    GameObjectLootParsing[] gameobjectHerbalismDatas = JsonConvert.DeserializeObject<GameObjectLootParsing[]>(gameobjectHerbalismJSon);
                    SetGameobjectHerbalismOrMiningData(gameobjectHerbalismDatas, Int32.Parse(gameobjectHerbalismTotalCount), true);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("mining"))
            {
                String gameobjectMiningPattern = @"new Listview\(\{template: 'item', id: 'mining',.*_totalCount: ([0-9]+),.*data: (.+)\}\);";

                String gameobjectMiningTotalCount = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectMiningPattern, 0);
                String gameobjectMiningJSon = Tools.ExtractJsonFromWithPattern(gameobjectHtml, gameobjectMiningPattern, 1);
                if (gameobjectMiningJSon != null)
                {
                    GameObjectLootParsing[] gameobjectMiningDatas = JsonConvert.DeserializeObject<GameObjectLootParsing[]>(gameobjectMiningJSon);
                    SetGameobjectHerbalismOrMiningData(gameobjectMiningDatas, Int32.Parse(gameobjectMiningTotalCount), false);
                    optionSelected = true;
                }
            }

            if (optionSelected)
                return true;
            else
                return false;
        }

        public void SetGameobjectLootData(GameObjectLootItemParsing[] gameobjectLootItemDatas, GameObjectLootCurrencyParsing[] gameobjectLootCurrencyDatas)
        {
            GameObjectLootParsing[] gameobjectLootDatas = new GameObjectLootParsing[gameobjectLootItemDatas.Length + gameobjectLootCurrencyDatas.Length];
            Array.Copy(gameobjectLootItemDatas, gameobjectLootDatas, gameobjectLootItemDatas.Length);
            Array.Copy(gameobjectLootCurrencyDatas, 0, gameobjectLootDatas, gameobjectLootItemDatas.Length, gameobjectLootCurrencyDatas.Length);

            List<String> modes = new List<String>() { "1", "2", "4", "33554432" };

            for (uint i = 0; i < gameobjectLootDatas.Length; ++i)
            {
                float count = 0.0f;
                float outof = 0.0f;
                float percent = 0.0f;

                foreach (String mode in modes)
                {
                    try
                    {
                        count = (float)Convert.ToDouble(gameobjectLootDatas[i].modes[mode]["count"]);
                        outof = (float)Convert.ToDouble(gameobjectLootDatas[i].modes[mode]["outof"]);
                        percent = count * 100 / outof;
                    }
                    catch (Exception) { }
                }

                GameObjectLootItemParsing currentItemParsing = null;
                try
                {
                    currentItemParsing = (GameObjectLootItemParsing)gameobjectLootDatas[i];
                }
                catch (Exception) { }

                gameobjectLootDatas[i].questRequired = currentItemParsing != null && currentItemParsing.classs == 12 ? "1": "0";

                // Normalize
                if (percent > 99.0f)
                    percent = 100.0f;

                gameobjectLootDatas[i].percent = Tools.NormalizeFloat(percent);
            }

            m_gameobjectLootDatas = gameobjectLootDatas;
        }

        public void SetGameobjectHerbalismOrMiningData(GameObjectLootParsing[] gameobjectHerbalismOrMiningDatas, int totalCount, bool herbalism)
        {
            for (uint i = 0; i < gameobjectHerbalismOrMiningDatas.Length; ++i)
            {
                float percent = (float)gameobjectHerbalismOrMiningDatas[i].count * 100 / (float)totalCount;

                gameobjectHerbalismOrMiningDatas[i].percent = Tools.NormalizeFloat(percent);
            }

            if (herbalism)
                m_gameobjectHerbalismDatas = gameobjectHerbalismOrMiningDatas;
            else
                m_gameobjectMiningDatas = gameobjectHerbalismOrMiningDatas;
        }

        public override String GetSQLRequest()
        {
            String returnSql = "";

            if (m_data.id == 0 || isError)
                return returnSql;

            if (IsCheckboxChecked("locale"))
            {
                LocaleConstant localeIndex = (LocaleConstant)Properties.Settings.Default.localIndex;

                if (localeIndex != 0)
                {
                    m_gameobjectLocalesBuilder = new SqlBuilder("gameobject_template_locale", "entry");
                    m_gameobjectLocalesBuilder.SetFieldsNames("locale", "name");

                    m_gameobjectLocalesBuilder.AppendFieldsValue(m_data.id, localeIndex.ToString(), m_data.name);
                    returnSql += m_gameobjectLocalesBuilder.ToString() + "\n";
                }
                else
                {
                    m_gameobjectLocalesBuilder = new SqlBuilder("gameobject_template", "entry");
                    m_gameobjectLocalesBuilder.SetFieldsNames("name");

                    m_gameobjectLocalesBuilder.AppendFieldsValue(m_data.id, m_data.name);
                    returnSql += m_gameobjectLocalesBuilder.ToString() + "\n";
                }
            }

            if (IsCheckboxChecked("loot") && m_gameobjectLootDatas != null)
            {
                m_gameobjectLootBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.DeleteInsert);
                m_gameobjectLootBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "";//"UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectLootData in m_gameobjectLootDatas)
                {
                    GameObjectLootCurrencyParsing currentLootCurrencyData = null;
                    try
                    {
                        currentLootCurrencyData = (GameObjectLootCurrencyParsing)gameobjectLootData;
                    }
                    catch (Exception ex) { }

                    int idMultiplier = currentLootCurrencyData != null ? -1 : 1;

                    if (idMultiplier < 1)
                        continue;

                    int minLootCount = gameobjectLootData.stack.Length >= 1 ? gameobjectLootData.stack[0] : 1;
                    int maxLootCount = gameobjectLootData.stack.Length >= 2 ? gameobjectLootData.stack[1] : minLootCount;


                    m_gameobjectLootBuilder.AppendFieldsValue(  m_data.id, // Entry
                                                                gameobjectLootData.id * idMultiplier, // Item
                                                                0, // Reference
                                                                gameobjectLootData.percent, // Chance
                                                                gameobjectLootData.questRequired, // QuestRequired
                                                                1, // LootMode
                                                                0, // GroupId
                                                                minLootCount, // MinCount
                                                                maxLootCount, // MaxCount
                                                                ""); // Comment
                }

                returnSql += m_gameobjectLootBuilder.ToString() + "\n";
            }

            if (IsCheckboxChecked("herbalism") && m_gameobjectHerbalismDatas != null)
            {
                m_gameobjectHerbalismBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_gameobjectHerbalismBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n"; //"UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectHerbalismData in m_gameobjectHerbalismDatas)
                    m_gameobjectHerbalismBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                   gameobjectHerbalismData.id, // Item
                                                                   0, // Reference
                                                                   gameobjectHerbalismData.percent, // Chance
                                                                   0, // QuestRequired
                                                                   1, // LootMode
                                                                   0, // GroupId
                                                                   gameobjectHerbalismData.stack[0], // MinCount
                                                                   gameobjectHerbalismData.stack[1], // MaxCount
                                                                   ""); // Comment

                returnSql += m_gameobjectHerbalismBuilder.ToString() + "\n";
            }

            if (IsCheckboxChecked("mining") && m_gameobjectMiningDatas != null)
            {
                m_gameobjectMiningBuilder = new SqlBuilder("gameobject_loot_template", "entry", SqlQueryType.InsertIgnore);
                m_gameobjectMiningBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                returnSql += "UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n"; //"UPDATE gameobject_template SET data1 = " + m_data.id + " WHERE entry = " + m_data.id + " AND type IN (3, 50);\n";
                foreach (GameObjectLootParsing gameobjectMiningData in m_gameobjectMiningDatas)
                    m_gameobjectMiningBuilder.AppendFieldsValue(m_data.id, // Entry
                                                                gameobjectMiningData.id, // Item
                                                                0, // Reference
                                                                gameobjectMiningData.percent, // Chance
                                                                0, // QuestRequired
                                                                1, // LootMode
                                                                0, // GroupId
                                                                gameobjectMiningData.stack[0], // MinCount
                                                                gameobjectMiningData.stack[1], // MaxCount
                                                                ""); // Comment

                returnSql += m_gameobjectMiningBuilder.ToString() + "\n";
            }

            return returnSql;
        }

        private GameObjectParsing m_data;
        protected GameObjectLootParsing[] m_gameobjectLootDatas;
        protected GameObjectLootParsing[] m_gameobjectHerbalismDatas;
        protected GameObjectLootParsing[] m_gameobjectMiningDatas;

        protected SqlBuilder m_gameobjectLootBuilder;
        protected SqlBuilder m_gameobjectHerbalismBuilder;
        protected SqlBuilder m_gameobjectMiningBuilder;
        protected SqlBuilder m_gameobjectLocalesBuilder;
    }
}
