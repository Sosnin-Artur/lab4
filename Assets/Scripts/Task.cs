using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor.PackageManager.Requests;
using UnityEditor.Search;
using System.Linq;

public class Task : MonoBehaviour
{
    [SerializeField]
    private float _timeInterval = 0.01f;
    [SerializeField]
    private TextMeshProUGUI _errorText;
    [SerializeField]
    private TextMeshProUGUI _resultText;


    private float _x1;
    private float _x2;
    private float _intensityX1;
    private float _intensityX2;    
    private float _time;

    private int _numberOfChannels = 0;
    private int _queueSize = 0;
    private float _requestX1intensity = 0;
    private float _requestX2Intensity = 0;
    private float _serviceX1Intensity = 0;
    private float _serviceX2Intensity = 0;
    private float _probabilityToServeTheRequestX1 = 0;
    private float _probabilityToServeTheRequestX2 = 0;

    private List<(int, float, float, int, float, float, int)?> _serviceChannels;
    private List<(int, float, int)> _queue;

    private List<(int, float, float, float, int)> _doneRequests;    
    private List<(int, int)> _rejectedRequests;
    private List<int> _cancelledRequests;

    private float _currentTime = 0;
    private int _requestNumber = 0;

    private float _timeForNewRequestX1 = 0;
    private float _timeForNewRequestX2 = 0;

    public void OnStartButton()
    {
        //StartProcess(1, 2, _x1, _x2, _intensityX1, _intensityX2, _intensityX1 / _x1, _intensityX2 / _x2, _time);
        StartProcess(1, 2, _x1, _x2, _intensityX1, _intensityX2, _x1 / _intensityX1, _x2 / _intensityX2, _time);
    }

    public void StartProcess(int n, int m, float x1, float x2, float mu1, float mu2, float p1, float p2, float run_life_time)
    {
        Init(n, m, x1, x2, mu1, mu2, p1, p2);
        
        _timeForNewRequestX1 = RequestX1TimeGenerator() + _currentTime;
        _timeForNewRequestX2 = RequestX2TimeGenerator() + _currentTime;

        while (_currentTime <= run_life_time)
        {
            _currentTime += _timeInterval;
            ProcessQueue();
            ProcessChannels();

            if (_timeForNewRequestX1 <= _currentTime)
            {
                CreateNewRequestX1();
                _timeForNewRequestX1 = RequestX1TimeGenerator() + _currentTime;
            }

            if (_timeForNewRequestX2 <= _currentTime)
            {
                CreateNewRequestX2();
                _timeForNewRequestX2 = RequestX2TimeGenerator() + _currentTime;
            }
        }

        ShowStatistic();
    }

    public void InputX1(string x1)
    {
        if (!float.TryParse(x1, out _x1))
        {
            _errorText.gameObject.SetActive(true);
            _errorText.text = "incorrect x1 input";
        }
        else
        {
            _errorText.gameObject.SetActive(false);
        }
    }

    public void InputX2(string x2)
    {
        if (!float.TryParse(x2, out _x2))
        {
            _errorText.gameObject.SetActive(true);
            _errorText.text = "incorrect x2 input";
        }
        else
        {
            _errorText.gameObject.SetActive(false);
        }
    }

    public void InputIntensityX1(string intensityX1)
    {
        if (!float.TryParse(intensityX1, out _intensityX1))
        {
            _errorText.gameObject.SetActive(true);
            _errorText.text = "incorrect intensityX1 input";
        }
        else
        {
            _errorText.gameObject.SetActive(false);
        }
    }

    public void InputIntensityX2(string intensityX2)
    {
        if (!float.TryParse(intensityX2, out _intensityX2))
        {
            _errorText.gameObject.SetActive(true);
            _errorText.text = "incorrect intensityX2 input";
        }
        else
        {
            _errorText.gameObject.SetActive(false);
        }
    }

    public void InputTime(string time)
    {
        if (!float.TryParse(time, out _time))
        {
            _errorText.gameObject.SetActive(true);
            _errorText.text = "incorrect time input";
        }
        else
        {
            _errorText.gameObject.SetActive(false);
        }
    }


    public void CreateNewRequestX1()
    {
        _requestNumber += 1;

        var requestId = _requestNumber;
        
        QueueRequest(requestId, 1);        
    }

    public void CreateNewRequestX2()
    {
        _requestNumber += 1;

        var requestId = _requestNumber;

        QueueRequest(requestId, 0);        
    }

    public void QueueRequest(int requestId, int priority)
    {
        if (priority == 1)
        {
            var seconds = _queue.FindAll(q => q.Item3 == 0);

            if (seconds.Count == 0)
            {
                if (_queue.Count < _queueSize)
                {
                    _queue.Add((requestId, _currentTime, priority));
                }
                else
                {
                    RejectRequest(requestId, 1);
                }
            }            
            else
            {
                var second = seconds[0];
                var ind = _queue.FindIndex(q => q == second);
                _queue[ind] = (requestId, _currentTime, priority);
                seconds.RemoveAt(0);
                
                QueueRequest(second.Item1, 0);
            }
            
        }
        else
        {
            if (_queue.Count < _queueSize)
            {

                _queue.Add((requestId, _currentTime, priority));
            }
            else
            {
                RejectRequest(requestId, 0);
            }
        }        
    }


    public void ProcessQueue() 
    {
        //var freeChannelIndex = FindFreeChannel();
        var freeChannelIndex = 0;

        if (_queue.Count == 0)
        {
            return;
        }

        if (freeChannelIndex == -1)
        {
            return;
        }


        var (requestId, startTime, priority) = _queue.ElementAt(0);
        _queue.RemoveAt(0);

        var timeInQueue = _currentTime - startTime;
        ServeRequest(requestId, startTime, timeInQueue, freeChannelIndex, priority);
    }
        
    public void ProcessChannels()
    {
        foreach (var channel in _serviceChannels)
        {            
            if (channel is null)
            {
                continue;
            }

            if (channel.Value.Item5 < _currentTime)
            {
                AcceptRequest(channel);
            }
            
        }
    }


    public void ServeRequest(int requestId, float startTime, float timeInQueue, int freeChannelIndex, int priority)
    {
        float serveTime = 0;

        if (priority == 1)
        {
            serveTime = ServeX1TimeGenerator();
        }
        else
        {
            serveTime = ServeX2TimeGenerator();
        }

        var end_time = serveTime + _currentTime;
        _serviceChannels[0] = 
            (requestId, startTime, timeInQueue, freeChannelIndex, serveTime, end_time, priority);
    }


    public int FindFreeChannel() 
    {
        if (_serviceChannels.Contains(null))
        {
            return _serviceChannels.IndexOf(null);
        }

        return -1;
    }


    public void AcceptRequest((int, float, float, int, float, float, int)? request)
    {
        FreeChannel(request.Value.Item4);
        
        if (request.Value.Item7 == 1)
        {
            if (_probabilityToServeTheRequestX1 >= UnityEngine.Random.value)
            {
                DoneRequest(request);
            }
            else
            {
                CancelRequest(request.Value.Item1);
                TryToProcessRequest(request.Value.Item1, 1);
            }
        }
        else
        {

            if (_probabilityToServeTheRequestX2 >= UnityEngine.Random.value)
            {
                DoneRequest(request);
            }
            else
            {
                CancelRequest(request.Value.Item1);
                TryToProcessRequest(request.Value.Item1, 0);
            }
        }

    }


    public void DoneRequest((int, float, float, int, float, float, int)? request)
    {
        var timeInQueuingSystem = _currentTime - request.Value.Item2 + request.Value.Item5;
        _doneRequests.Add((request.Value.Item1, request.Value.Item3, request.Value.Item5, timeInQueuingSystem, request.Value.Item7));
    }

    public void RejectRequest(int requestId, int priority)
    {
        _rejectedRequests.Add((requestId, priority));
    }


    public void CancelRequest(int requestId)
    {
        _cancelledRequests.Add(requestId);
    }


    public void FreeChannel(int index)
    {
        //_serviceChannels[index] = null;
    }


    public void TryToProcessRequest(int requestId, int priority)
    {        
        QueueRequest(requestId, priority);                  
    }


    public void Init(int n, int m, float x1, float x2, float mu1, float mu2, float p1, float p2)
    {
        _serviceChannels = new();
        _queue = new();
        _doneRequests = new();
        _rejectedRequests = new();
        _cancelledRequests = new();
        _currentTime = 0;
        _numberOfChannels = n;
        _queueSize = m;
        _requestX1intensity = x1;
        _requestX2Intensity = x2;
        _serviceX1Intensity = mu1;
        _serviceX2Intensity = mu2;
        _serviceChannels = new();
        
        for (int i = 0; i < n; i++)
        {
            _serviceChannels.Add(null);
        }
        _probabilityToServeTheRequestX1 = p1;
        _probabilityToServeTheRequestX2 = p2;
    }


    public float RequestX1TimeGenerator()
    {
        return ExponentialValueGenerator(_requestX1intensity);
    }

    public float RequestX2TimeGenerator()
    {
        return ExponentialValueGenerator(_requestX2Intensity);
    }

    public float ServeX1TimeGenerator()
    {
        return ExponentialValueGenerator(_serviceX1Intensity);
    }

    public float ServeX2TimeGenerator()
    {
        return ExponentialValueGenerator(_serviceX2Intensity);
    }


    public float ExponentialValueGenerator(float intensity)
    {
        return (intensity * Mathf.Log(1 / (1 - UnityEngine.Random.value)));
    }

    public (float, float) GetAverageServiceRequestTime()
    {
        var averageX1 = new List<float>();
        var averageX2 = new List<float>();

        for (int i = 0; i < _doneRequests.Count; i++)
        {
            if (_doneRequests[i].Item5 == 1)
            {
                averageX1.Add(_doneRequests[i].Item3);
            }
            else
            {
                averageX2.Add(_doneRequests[i].Item3);
            }
        }

        return (Mean(averageX1), Mean(averageX2));
    }        

    public (float, float) GetAverageQueueTime()
    {
        var averageX1 = new List<float>();
        var averageX2 = new List<float>();

        for (int i = 0; i < _doneRequests.Count; i++)
        {
            if (_doneRequests[i].Item5 == 1)
            {
                averageX1.Add(_doneRequests[i].Item2);
            }
            else
            {
                averageX2.Add(_doneRequests[i].Item2);
            }
        }

        return (Mean(averageX1), Mean(averageX2));
    }


    public (float, float) GetAverageRequestTimeInSystem()
    {
        var averageRequestTimeInSystemX1 = new List<float>();
        var averageRequestTimeInSystemX2 = new List<float>();

        for (int i = 0; i < _doneRequests.Count; i++)
        {
            if (_doneRequests[i].Item5 == 1)
            {
                averageRequestTimeInSystemX1.Add(_doneRequests[i].Item4);
            }            
            else
            {
                averageRequestTimeInSystemX2.Add(_doneRequests[i].Item4);
            }
        }

        return (Mean(averageRequestTimeInSystemX1), Mean(averageRequestTimeInSystemX2));
    }

    public float Mean(List<float> list)
    {
        if (list.Count == 0)
        {
            return 0;
        }

        var sum = 0.0f;
        foreach (var item in list)
        {
            sum += item;
        }

        return sum / list.Count;
    }

    public float Mean(List<int> list)
    {
        if (list.Count == 0)
        {
            return 0;
        }

        var sum = 0.0f;
        foreach (var item in list)
        {
            sum += item;
        }

        return sum / list.Count;
    }

    public void ShowStatistic()
    {        
        var (averageServiceRequestX1Time, averageServiceRequestX2Time) = GetAverageServiceRequestTime();
        var (averageQueueTimeX1, averageQueueTimeX2) = GetAverageQueueTime();
        var (averageRequestTimeInSystemX1, averageRequestTimeInSystemX2) = GetAverageRequestTimeInSystem();

        var text = "";
        Debug.Log($"Количество выполненных X1: {_doneRequests.FindAll(r => r.Item5 == 1).Count}");
        Debug.Log($"Количество выполненных X2: {_doneRequests.FindAll(r => r.Item5 == 0).Count}");
        Debug.Log($"Количество отклоненных X1: {_rejectedRequests.FindAll(r => r.Item2 == 1).Count}");
        Debug.Log($"Количество отклоненных X2: {_rejectedRequests.FindAll(r => r.Item2 == 0).Count}");
        Debug.Log($"Вероятность выполнить X1: {(float)_doneRequests.FindAll(r => r.Item5 == 1).Count / (float)(_doneRequests.FindAll(r => r.Item5 == 1).Count + _rejectedRequests.FindAll(r => r.Item2 == 1).Count)}");
        Debug.Log($"Вероятность выполнить X2: {(float)_doneRequests.FindAll(r => r.Item5 == 0).Count / (float)(_doneRequests.FindAll(r => r.Item5 == 0).Count + _rejectedRequests.FindAll(r => r.Item2 == 0).Count)}");
        Debug.Log($"Среднее время заявки X1 под обслуживанием : {averageServiceRequestX1Time}");
        Debug.Log($"Среднее время заявки X2 под обслуживанием : {averageServiceRequestX2Time}");
        Debug.Log($"Среднее время заявки X1 в очереди : {averageQueueTimeX1}");
        Debug.Log($"Среднее время заявки X2 в очереди : {averageQueueTimeX2}");
        Debug.Log($"Среднее время заявки X1 в системе : {averageRequestTimeInSystemX1}");
        Debug.Log($"Среднее время заявки X2 в системе : {averageRequestTimeInSystemX2}");
        Debug.Log("   ");

        text += $"Количество выполненных X1: {_doneRequests.FindAll(r => r.Item5 == 1).Count}\n";
        text += $"Количество выполненных X2: {_doneRequests.FindAll(r => r.Item5 == 0).Count}\n";
        text += $"Количество отклоненных X1: {_rejectedRequests.FindAll(r => r.Item2 == 1).Count}\n";
        text += $"Количество отклоненных X2: {_rejectedRequests.FindAll(r => r.Item2 == 0).Count}\n";
        text += $"Вероятность выполнить X1: {(float)_doneRequests.FindAll(r => r.Item5 == 1).Count / (float)(_doneRequests.FindAll(r => r.Item5 == 1).Count + _rejectedRequests.FindAll(r => r.Item2 == 1).Count)}\n";
        text += $"Вероятность выполнить X2: {(float)_doneRequests.FindAll(r => r.Item5 == 0).Count / (float)(_doneRequests.FindAll(r => r.Item5 == 0).Count + _rejectedRequests.FindAll(r => r.Item2 == 0).Count)}\n";
        text += $"Среднее время заявки X1 под обслуживанием : {averageServiceRequestX1Time}\n";
        text += $"Среднее время заявки X2 под обслуживанием : {averageServiceRequestX2Time}\n";
        text += $"Среднее время заявки X1 в очереди : {averageQueueTimeX1}\n";
        text += $"Среднее время заявки X2 в очереди : {averageQueueTimeX2}\n";
        text += $"Среднее время заявки X1 в системе : {averageRequestTimeInSystemX1}\n";
        text += $"Среднее время заявки X2 в системе : {averageRequestTimeInSystemX2}\n";        

        _resultText.text = text;
    }
}
