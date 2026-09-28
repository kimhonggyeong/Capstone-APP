using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FinanceQuizData
{
    public string id;
    public string category;
    public string difficulty;
    public string type;
    public string question;
    public List<string> options;
    public int answerIndex;
    public string explanation;
    public string relatedTermId;
}