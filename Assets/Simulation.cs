using UnityEngine;
using Random = System.Random;

public class Simulation : MonoBehaviour
{
    public readonly struct Agent
    {
        public Vector2 Position { get; }
        public float Angle { get; }

        public static int SizeOf => System.Runtime.InteropServices.Marshal.SizeOf(typeof(Agent));

        public Agent(Vector2 position, float angle)
        {
            Position = position;
            Angle = angle;
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


        if (_agents == null || _agents.Length != _agentsCount)
        {
            InitializeAgentsBuffer();
        }
        _computeShader.SetFloat("width", _width);
        _computeShader.SetFloat("height", _height);
    }

    protected void Update()
    {
        _computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        _computeShader.Dispatch(_kernelIndex, _width, _height, _agents.Length);
    }

    private void InitializeAgentsBuffer()
    {
        _agents = new Agent[_agentsCount];

        for (int i = 0; i < _agentsCount; i++)
        {
            _agents[i] = new Agent(GetRandomPosition(), (float)_random.NextDouble() * 2 * Mathf.PI);
        }

        _buffer?.Dispose();

        _buffer = new ComputeBuffer(_agents.Length, Agent.SizeOf);
        _buffer.SetData(_agents);
        _computeShader.SetBuffer(_kernelIndex, "agents", _buffer);
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
    private ComputeBuffer _buffer;

    private Vector2 GetRandomVecNormalized() => new Vector2(RandomSign() * (float)_random.NextDouble(),
        RandomSign() * (float)_random.NextDouble());

    float RandomSign() => (float)_random.NextDouble() > 0.5f ? 1 : -1;

    private Vector2Int GetRandomPosition()
    {
        int x = _random.Next(0, _width);
        int y = _random.Next(0, _height);
        return new Vector2Int(x, y);
    }
}