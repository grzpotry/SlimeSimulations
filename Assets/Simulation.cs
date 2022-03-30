using System;
using UnityEngine;
using Random = System.Random;

public class Simulation : MonoBehaviour
{
    public readonly struct Agent
    {
        public Vector2 Position { get; }

        public static int Stride => System.Runtime.InteropServices.Marshal.SizeOf(typeof(Agent));

        public Agent(Vector2 position)
        {
            Position = position;
        }
    }

    protected void Start()
    {
        _renderTexture = new RenderTexture(_width, _height, 24);
        _renderTexture.enableRandomWrite = true;
        _renderTexture.Create();

        _kernelIndex = _computeShader.FindKernel("CSMain");
        _computeShader.SetTexture(_kernelIndex, "Result", _renderTexture);
        _outputRenderer.material.mainTexture = _renderTexture;
    }

    protected void Update()
    {
        if (_agents == null || _agents.Length != _agentsCount)
        {
            InitializeAgentsBuffer();
        }

        _computeShader.Dispatch(_kernelIndex, _width, _height, _agents.Length);
    }

    private void InitializeAgentsBuffer()
    {
        _agents = new Agent[_agentsCount];

        for (int i = 0; i < _agentsCount; i++)
        {
            _agents[i] = new Agent(GetRandomPosition());
        }

        var buffer = new ComputeBuffer(_agents.Length, Agent.Stride);
        buffer.SetData(_agents);
        _computeShader.SetBuffer(_kernelIndex, "agents", buffer);
    }

    [SerializeField]
    private int _width = 256;

    [SerializeField]
    private int _height = 256;

    [SerializeField]
    private int _agentsCount = 25;

    [SerializeField]
    private ComputeShader _computeShader;

    [SerializeField]
    private MeshRenderer _outputRenderer;

    [SerializeField]
    private RenderTexture _renderTexture;

    private int _kernelIndex;
    private readonly Random _random = new Random();

    private Agent[] _agents;

    private Vector2 GetRandomPosition()
    {
        int x = _random.Next(0, _width);
        int y = _random.Next(0, _height);
        return new Vector2(x, y);
    }
}