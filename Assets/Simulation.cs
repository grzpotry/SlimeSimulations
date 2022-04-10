using UnityEngine;
using Random = System.Random;

public class Simulation : MonoBehaviour
{
    public readonly struct Agent
    {
        public Vector2 Position { get; }
        public float AngleRad { get; }

        public static int SizeOf => System.Runtime.InteropServices.Marshal.SizeOf(typeof(Agent));

        public Agent(Vector2 position, float angleRad)
        {
            Position = position;
            AngleRad = angleRad;
        }
    }

    protected void Awake()
    {
        _rawTexture = new RenderTexture(_width, _height, 24);
        _rawTexture.enableRandomWrite = true;
        _rawTexture.Create();

        _diffusedTexture = new RenderTexture(_width, _height, 24);
        _diffusedTexture.enableRandomWrite = true;
        _diffusedTexture.Create();

        _mainKernelIndex = _computeShader.FindKernel("UpdateMovement");
        _computeShader.SetTexture(_mainKernelIndex, "Result", _rawTexture);

        _senseKernelIndex = _computeShader.FindKernel("UpdateSensors");
        _computeShader.SetTexture(_senseKernelIndex, "Result", _rawTexture);

        _clearKernelIndex = _computeShader.FindKernel("Clear");
        _computeShader.SetTexture(_clearKernelIndex, "Result", _rawTexture);

        _blurKernelIndex = _computeShader.FindKernel("Blur");
        _computeShader.SetTexture(_blurKernelIndex, "DiffusedTex", _diffusedTexture);
        _computeShader.SetTexture(_blurKernelIndex, "Result", _rawTexture);

        _evaporationKernelIndex = _computeShader.FindKernel("Evaporation");
        _computeShader.SetTexture(_evaporationKernelIndex, "Result", _rawTexture);

        _outputRenderer.material.mainTexture = _diffusedTexture;

        if (_agents == null || _agents.Length != _agentsCount)
        {
            InitializeAgentsBuffer();
        }

        _computeShader.SetFloat("width", _width);
        _computeShader.SetFloat("height", _height);

        RunSimulation();
    }

    protected void OnGUI()
    {
    }

    protected void Update() //todo: Fixed
    {
        RunSimulation();
        foreach (var agent in _agents)
        {
            var heading = new Vector2(Mathf.Cos(agent.AngleRad), Mathf.Sin(agent.AngleRad));
            var end = agent.Position + heading * +_debugHeadingRayLength;
            Debug.DrawLine(agent.Position, end, Color.red);
        }
    }

    private void RunSimulation()
    {
        _computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        _computeShader.SetFloat("time", Time.time);
        _computeShader.SetFloat("speed", _agentsSpeed);
        _computeShader.SetFloat("evaporationSpeed", _evaporationSpeed);
        _computeShader.SetInt("sensorWidth", _sensorWidth);
        _computeShader.SetFloat("sensorAngle", _sensorAngleDeg * Mathf.Deg2Rad);
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

        _computeShader.Dispatch(_evaporationKernelIndex, _width, _height, 1);
        _computeShader.Dispatch(_blurKernelIndex, _width, _height, 1);
    }

    private void InitializeAgentsBuffer()
    {
        _agents = new Agent[_agentsCount];

        for (int i = 0; i < _agentsCount; i++)
        {
            var randomPosition = GetRandomPosition();
            var center = new Vector2((_width / 2.0f), (_height / 2.0f));
            var heading = (center - randomPosition).normalized;
            var angleRad = Mathf.Atan2(heading.y, heading.x);
            _agents[i] = new Agent(randomPosition, angleRad);
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
    private int _debugHeadingRayLength = 2;

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
    private RenderTexture _rawTexture;

    private RenderTexture _diffusedTexture;

    [SerializeField]
    private float _sensorAngleDeg;

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

    private int _evaporationKernelIndex;
    private int _blurKernelIndex;
    private int _senseKernelIndex;
    private int _mainKernelIndex;
    private int _clearKernelIndex;
    private readonly Random _random = new Random();

    private Agent[] _agents;
    private ComputeBuffer _buffer;

    private Vector2 GetRandomVecNormalized() => new Vector2(RandomSign() * (float)_random.NextDouble(),
        RandomSign() * (float)_random.NextDouble());

    float RandomSign() => (float)_random.NextDouble() > 0.5f ? 1 : -1;

    private Vector2 GetRandomPosition()
    {
        int x = _random.Next(0, _width);
        int y = _random.Next(0, _height);
        return new Vector2(x, y);
    }
}