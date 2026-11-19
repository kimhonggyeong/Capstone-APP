using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FinanceTermData
{
    public string id;
    public string term;
    public string category;
    public string difficulty;
    public string shortDefinition;
    public string description;
    public string example;
    public List<string> keyPoints;
    public string caution;
    public List<string> relatedTerms;
}