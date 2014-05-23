﻿namespace TMT.Rule
{
    using System;
    using System.Collections.Generic;
    using MongoDB.Driver;
    using TMT.Mongolian;

    /// <summary>
    /// Singleton class
    /// </summary>
    public class MongolianGenerator
    {
        private List<MongolianSuffix> _suffixes;  // Нөхцөл|Дагавар
        private MongolianWord _root;    // Язгуур
        private MongolianWord _resultWord;  // Залгагдсан үг
        private static MongolianGenerator _instance;
        private List<MongolianGeneratorResult> _results;
        private MongolianGenerator() {} // Locking the constructor

        /// <summary>
        /// Returns the instance of the MongolianGenerator
        /// </summary>
        public static MongolianGenerator Instance
        {
            get 
            {
                if (_instance == null)
                {
                    _instance = new MongolianGenerator();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets and Sets the _suffixes
        /// </summary>
        public List<MongolianSuffix> Suffixes
        {
            get
            {
                return _suffixes;
            }
            set
            {
                _suffixes = value;
            }
        }

        /// <summary>
        /// Gets and Sets the _root
        /// </summary>
        public MongolianWord Root
        {
            get
            {
                return _root;
            }
            set
            {
                _root = value;
            }
        }

        /// <summary>
        /// Gets and Sets the _resultWord
        /// </summary>
        public MongolianWord ResultWord
        {
            get
            {
                return _resultWord;
            }
            set
            {
                _resultWord = value;
            }
        }

        /// <summary>
        /// Gets and Sets the _results
        /// </summary>
        public List<MongolianGeneratorResult> Results
        {
            get
            {
                return _results;
            }
            set
            {
                _results = value;
            }
        }

        /// <summary>
        /// Generates the appended word
        /// </summary>
        public string Generate(String root, List<String> suffixes)
        {
            Root = new MongolianWord();
            Root.Word = root;

            Suffixes = new List<MongolianSuffix>();
            for (int i = 0; i < suffixes.Count; i++)
            {
                MongolianSuffix newSuffix = new MongolianSuffix();
                newSuffix.Word = suffixes[i];
                Suffixes.Add(newSuffix);
            }

            MongoCursor<GeneratorRule> ruleTableCursor = Mongo.Instance.Database.GetCollection<GeneratorRule>("GeneratorRuleTable").FindAll();
            Results = new List<MongolianGeneratorResult>();

            // Prioritizing the suffixes and reordering the list
            for (int i = 0; i < Suffixes.Count; i++)
            {
                Boolean exists = false;
                foreach (var rule in ruleTableCursor)
                {
                    if (rule.Suffix.Word.Equals(Suffixes[i].Word))
                    {
                        Suffixes[i].Priority = rule.Priority;
                        exists = true; break;
                    }
                }
                if (exists == false) Suffixes[i].Priority = -1;
            }

            MongolianSuffix temp;
            for (int i = 0; i < Suffixes.Count; i++)
            {
                for (int j = i + 1; j < Suffixes.Count; j++)
                {
                    if (Suffixes[i].Priority == -1 && Suffixes[j].Priority != -1)
                    {
                        temp = Suffixes[i];
                        Suffixes[i] = Suffixes[j];
                        Suffixes[j] = temp;
                    }
                    else if (Suffixes[i].Priority > Suffixes[j].Priority)
                    {
                        temp = Suffixes[i];
                        Suffixes[i] = Suffixes[j];
                        Suffixes[j] = temp;
                    }
                }
            }

            // Applying the rules
            ResultWord = Root;
            string begin;
            GeneratorRule chosenRule = null;

            for (int i = 0; i < Suffixes.Count; i++)
            {
                begin = "";
                MongolianGeneratorResult tempResult = new MongolianGeneratorResult();
                tempResult.Root = ResultWord.Word;

                for(int j = ResultWord.Word.Length - 1; j >= 0; j--)
                {
                    Boolean exists = false;
                    foreach (var rule in ruleTableCursor)
                    {
                        if(rule.Root.Word.EndsWith(begin) && rule.Suffix.Word.StartsWith(Suffixes[i].Word.Substring(0,1)))
                        {
                            exists = true;
                            chosenRule = rule;
                            break;
                        }
                    }

                    if (exists == false && chosenRule != null)
                    {
                        for (int k = 1; k < Suffixes[i].Word.Length; k++)
                        {
                            exists = false;
                            foreach (var rule in ruleTableCursor)
                            {
                                if (rule.Root.Word.EndsWith(begin) && rule.Suffix.Word.StartsWith(Suffixes[i].Word.Substring(0, k + 1)))
                                {
                                    exists = true;
                                    chosenRule = rule;
                                    break;
                                }
                            }

                            if (exists == false)
                            {
                                // Язгуурын төгсгөл солих
                                if (chosenRule.RootChangePart != null && chosenRule.RootChangePart.Word.Length > 0)
                                {
                                    if (chosenRule.RootChangeRule != null && chosenRule.RootChangeRule.Word.Length > 0) ResultWord.Word = ResultWord.Word.Replace(chosenRule.RootChangePart.Word, chosenRule.RootChangeRule.Word);
                                }
                                // Нөхцөл|Дагаварын эхлэл солих
                                if (chosenRule.SuffixChangePart != null && chosenRule.SuffixChangePart.Word.Length > 0)
                                {
                                    if (chosenRule.SuffixChangeRule != null && chosenRule.RootChangeRule.Word.Length > 0) Suffixes[i].Word = Suffixes[i].Word.Replace(chosenRule.SuffixChangePart.Word, chosenRule.SuffixChangeRule.Word);
                                }
                                // Жийрэглэх
                                if (chosenRule.Middle != null && chosenRule.Middle.Word.Length > 0)
                                {
                                    ResultWord.Word += chosenRule.Middle.Word;
                                }
                                ResultWord.Word += Suffixes[i].Word;
                                break;
                            }
                        }
                    }
                    else
                    {
                        ResultWord.Word += Suffixes[i].Word;
                        break;
                    }
                    begin = ResultWord.Word.Substring(j);
                }
                if (chosenRule == null)
                {
                    tempResult.Rule = new GeneratorRule();
                }
                else tempResult.Rule = chosenRule;
                tempResult.Result = ResultWord.Word;
                tempResult.Suffix = Suffixes[i].Word;
                Results.Add(tempResult);
            }
            return ResultWord.Word;
        }
    }
}