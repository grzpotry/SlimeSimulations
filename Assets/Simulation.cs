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

        _movementKernelIndex = _computeShader.FindKernel("UpdateMovement");
        _senseKernelIndex = _computeShader.FindKernel("UpdateSensors");
        _clearKernelIndex = _computeShader.FindKernel("Clear");
        _blurKernelIndex = _computeShader.FindKernel("Blur");
        _evaporationKernelIndex = _computeShader.FindKernel("Evaporation");

        if (_agents == null || _agents.Length != _agentsCount)
        {
            InitializeAgentsBuffer();
        }

        RunSimulation();
    }

    protected void OnGUI()
    {
    }

    protected void FixedUpdate() //todo: Fixed
    {
        RunSimulation();
    }

    private void RunSimulation()
    {
        _outputRenderer.material.mainTexture = _diffusedTexture;

        if (_showRawTexture)
        {
            _outputRenderer.material.mainTexture = _rawTexture;
        }
        else
        {
            _outputRenderer.material.mainTexture = _diffusedTexture;
        }

        _computeShader.SetFloat("width", _width);
        _computeShader.SetFloat("height", _height);
        _computeShader.SetTexture(_movementKernelIndex, "Result", _rawTexture);
        _computeShader.SetTexture(_senseKernelIndex, "Result", _rawTexture);
        _computeShader.SetTexture(_clearKernelIndex, "Result", _rawTexture);
        _computeShader.SetTexture(_blurKernelIndex, "DiffusedTex", _diffusedTexture);
        _computeShader.SetTexture(_blurKernelIndex, "Result", _rawTexture);
        _computeShader.SetTexture(_evaporationKernelIndex, "Result", _rawTexture);
        _computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        _computeShader.SetFloat("time", Time.time);
        _computeShader.SetFloat("speed", _agentsSpeed);
        _computeShader.SetFloat("evaporationSpeed", _evaporationSpeed);
        _computeShader.SetInt("sensorWidth", _sensorWidth);
        _computeShader.SetFloat("sensorAngleRad", _sensorAngleDeg * Mathf.Deg2Rad);
        _computeShader.SetFloat("sensorOffsetDistance", _sensorOffsetDistance);
        _computeShader.SetFloat("diffuseSpeed", _diffuseSpeed);
        _computeShader.SetInt("agentsCount", _agents.Length);
        _computeShader.SetFloat("trailRate", _trailWeight);

        // _computeShader.Dispatch(_clearKernelIndex, _width, _height, 1);

        _computeShader.GetKernelThreadGroupSizes(_senseKernelIndex, out _, out _, out var z);
        _computeShader.GetKernelThreadGroupSizes(_movementKernelIndex, out var x, out _, out _);

        var groupsX =  Mathf.CeilToInt(_agents.Length / (float)x);
        var groupsZ =  Mathf.CeilToInt(_agents.Length / (float)z);

        for (int i = 0; i < _stepsPerFrame; i++)
        {
            _computeShader.Dispatch(_movementKernelIndex, groupsX, 1, 1);

            if (_sensoryStage)
            {
                _computeShader.Dispatch(_senseKernelIndex, 1, 1, groupsZ);
            }
        }

        _computeShader.Dispatch(_evaporationKernelIndex, _width, _height, 1);
        _computeShader.Dispatch(_blurKernelIndex, _width, _height, 1);

        Graphics.Blit(_diffusedTexture, _rawTexture);
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
        _computeShader.SetBuffer(_movementKernelIndex, "agents", _buffer);
        _computeShader.SetBuffer(_senseKernelIndex, "agents", _buffer);
    }

    [SerializeField]
    private bool _showRawTexture = false;

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
    private float _trailWeight = 1;

    [SerializeField]
    private bool _sensoryStage = true;

    private int _evaporationKernelIndex;
    private int _blurKernelIndex;
    private int _senseKernelIndex;
    private int _movementKernelIndex;
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