using UnityEngine;
//电子邮件puhalskijsemen@gmail.com
//源码网站 开vpn全局模式打开 https://web3incubators.com/
//电报https://t.me/gamecode999
//网页客服 https://web3incubators.com/kefu.html
[RequireComponent(typeof(LineRenderer))]
public class Rope : MonoBehaviour
{
    public Transform startPoint;       // Rope start (player)
    public Transform endPoint;         // Rope end (hook/target)
    public int segmentCount;           // Smoothness
    public float waveAmplitude = 0.2f; // Snake wiggle height
    public float waveFrequency = 2f;   // How many waves along rope
    public float waveSpeed = 3f;       // How fast waves move
    public float yOffset = -1f;        // Rope sag

    private LineRenderer line;
    [HideInInspector] public bool drawRope;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = segmentCount;
        line.useWorldSpace = true;
    }

    void Update()
    {
        if(drawRope)
            DrawRope();
    }

    void DrawRope()
    {
        Vector3 p0 = startPoint.position;
        Vector3 p2 = endPoint.position;

        // Mid control point for sag
        Vector3 p1 = (p0 + p2) / 2 + new Vector3(0, yOffset, 0);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);

            // Quadratic Bezier curve
            Vector3 curvePos = (1 - t) * (1 - t) * p0 +
                               2 * (1 - t) * t * p1 +
                               t * t * p2;

            // Wave effect strength (fade at ends, max in middle)
            float fade = Mathf.Sin(t * Mathf.PI); // 0 at ends, 1 in middle

            // Sideways direction for wave offset
            Vector3 ropeDir = (p2 - p0).normalized;
            Vector3 side = Vector3.Cross(ropeDir, Vector3.up).normalized;

            // Apply wave only in middle
            float wave = Mathf.Sin(Time.time * waveSpeed + t * waveFrequency * Mathf.PI * 2f)
                         * waveAmplitude * fade;

            curvePos += side * wave;

            line.SetPosition(i, curvePos);
        }
    }
}
