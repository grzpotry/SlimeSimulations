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

    protected void Awake()
    {
        _renderTexture = new RenderTexture(_width, _height, 24);
        _renderTexture.enableRandomWrite = true;
        _renderTexture.Create();

        _mainKernelIndex = _computeShader.FindKernel("UpdateMovement");
        _computeShader.SetTexture(_mainKernelIndex, "Result", _renderTexture);

        _senseKernelIndex = _computeShader.FindKernel("UpdateSensors");
        _computeShader.SetTexture(_senseKernelIndex, "Result", _renderTexture);

        _evaporateKernelIndex = _computeShader.FindKernel("EvaporateTrail");
        _computeShader.SetTexture(_evaporateKernelIndex, "Result", _renderTexture);

        _clearKernelIndex = _computeShader.FindKernel("Clear");
        _computeShader.SetTexture(_clearKernelIndex, "Result", _renderTexture);

        _blurKernelIndex = _computeShader.FindKernel("Blur");
        _computeShader.SetTexture(_blurKernelIndex, "Result", _renderTexture);

        _outputRenderer.material.mainTexture = _renderTexture;

        if (_agents == null || _agents.Length != _agentsCount)
        {
            InitializeAgentsBuffer();
        }

        _computeShader.SetFloat("width", _width);
        _computeShader.SetFloat("height", _height);
    }

    protected void Update() //todo: Fixed
    {
        _computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        _computeShader.SetFloat("time", Time.time);
        _computeShader.SetFloat("speed", _agentsSpeed);
        _computeShader.SetFloat("evaporationSpeed", _evaporationSpeed);
        _computeShader.SetInt("sensorWidth", _sensorWidth);
        _computeShader.SetFloat("sensorAngle", _sensorAngle);
        _computeShader.SetFloat("sensorOffsetDistance", _sensorOffsetDistance);
        _computeShader.SetFloat("diffuseSpeed", _diffuseSpeed);

        // _computeShader.Dispatch(_clearKernelIndex, _width, _height, 1);

        for (int i = 0; i < _stepsPerFrame; i++)
        {
            _computeShader.Dispatch(_mainKernelIndex, _agents.Length, 1, 1);

            if (_sensoryStage)
            {
                _computeShader.Dispatch(_senseKernelIndex, 1, 1, _agents.Length);
            }
        }

        _computeShader.Dispatch(_blurKernelIndex, _width, _height, 1);
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
        _computeShader.SetBuffer(_mainKernelIndex, "agents", _buffer);
        _computeShader.SetBuffer(_senseKernelIndex, "agents", _buffer);
    }

    [SerializeField]
    private int _width = 256;

    [SerializeField]
    private int _stepsPerFrame = 1;

    [SerializeField]
    private int _height = 256;

    [SerializeField]
    private int _agentsCount = 25;

    [SerializeField]
    private float _agentsSpeed = 1;

    [SerializeField]
    private ComputeShader _computeShader;

    [SerializeField]
    private MeshRenderer _outputRenderer;

    [SerializeField]
    private RenderTexture _renderTexture;

    [SerializeField]
    private float _sensorAngle;

    [SerializeField]
    private int _sensorWidth;

    [SerializeField]
    private float _sensorOffsetDistance;

    [SerializeField]
    private float _evaporationSpeed = 1;

    [SerializeField]
    private float _diffuseSpeed = 1;

    [SerializeField]
    private bool _sensoryStage = true;

    private int _blurKernelIndex;
    private int _senseKernelIndex;
    private int _mainKernelIndex;
    private int _clearKernelIndex;
    private int _evaporateKernelIndex;
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