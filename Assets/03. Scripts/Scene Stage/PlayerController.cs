using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Babel
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("발사 세기")]
        [SerializeField] private float forceMultiplier = 30.0f;
        [SerializeField] private float maxForce = 150.0f;

        // 앞으로 던지는 최소 수평 드래그값 (뒤로 당겨도 이 값만큼은 앞으로 날아감)
        private const float MinLaunchDragX = 10.0f;

        [Header("궤적 표시")]
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private int trajectoryPointCount = 30;
        [SerializeField] private float timeStepPerPoint = 0.1f;

        [Header("블록 생성 위치")]
        [SerializeField] private Transform spawnPoint;

        private Block currentBlock;
        private Vector2 dragStartPos;
        private bool isDragging;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;

            if (trajectoryLine == null) trajectoryLine = GetComponent<LineRenderer>();
            trajectoryLine.positionCount = trajectoryPointCount;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                trajectoryLine.material = new Material(spriteShader);
            }

            trajectoryLine.startWidth = 0.25f;
            trajectoryLine.endWidth = 0.01f;
            Gradient orangeGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            Color orangeColor = new Color(1.0f, 0.5f, 0.0f);
            Color yellowColor = new Color(1.0f, 1.0f, 0.0f);
            colorKeys[0] = new GradientColorKey(orangeColor, 0.0f);
            colorKeys[1] = new GradientColorKey(yellowColor, 1.0f);
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.75f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f);
            orangeGradient.SetKeys(colorKeys, alphaKeys);
            trajectoryLine.colorGradient = orangeGradient;
            trajectoryLine.numCornerVertices = 5;
            trajectoryLine.numCapVertices = 5;
            trajectoryLine.enabled = false;
            SpawnBlock();
        }

        private void Update()
        {
            if (IsGameplayBlocked())
            {
                if (trajectoryLine.enabled)
                {
                    trajectoryLine.enabled = false;
                }

                isDragging = false;
                return;
            }

            if (currentBlock == null)
            {
                if (trajectoryLine.enabled) trajectoryLine.enabled = false;
                isDragging = false;
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
                StartDrag();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (isDragging)
                {
                    FireBlock();
                }
            }
            else if (Mouse.current.leftButton.isPressed && isDragging)
            {
                OnDragging();
            }
        }

        private void StartDrag()
        {
            dragStartPos = GetMouseWorldPosition();
            isDragging = true;
            trajectoryLine.enabled = true;
        }

        private void OnDragging()
        {
            if (isDragging == false) return;

            Rigidbody2D blockRigidbody = currentBlock.GetComponent<Rigidbody2D>();
            forceMultiplier = blockRigidbody.mass / 1.5f;
            maxForce = blockRigidbody.mass * 30;
            Vector2 currentMousePos = GetMouseWorldPosition();
            Vector2 dragVector = dragStartPos - currentMousePos;
            UIManager.Instance.SetCameraTarget(currentBlock.transform, UIManager.Instance.ThrowerOffset, 25f);

            if (currentBlock != null)
            {
                currentBlock.transform.localPosition = Vector3.zero;
            }

            Vector2 predictedForce = CalculateLaunchForce(dragVector);

            float blockMass = currentBlock != null ? currentBlock.GetComponent<Rigidbody2D>().mass : 1.0f;
            Vector2 initialVelocity = predictedForce / blockMass;
            DrawTrajectory(spawnPoint.position, initialVelocity);
        }

        private void FireBlock()
        {
            isDragging = false;
            trajectoryLine.enabled = false;

            if (currentBlock == null) return;

            Vector2 currentMousePos = GetMouseWorldPosition();
            Vector2 dragVector = dragStartPos - currentMousePos;

            Vector2 shootForce = CalculateLaunchForce(dragVector);

            currentBlock.Launch(shootForce, GameManager.Instance.CurrentWindSpeed);
            UIManager.Instance.SetCameraTarget(currentBlock.transform, UIManager.Instance.BlockOffset, 18f);
            StartCoroutine(SpawnDelayCoroutine(currentBlock));
            currentBlock = null;
        }

        // 조준 궤적(OnDragging)과 실제 발사(FireBlock)가 같은 보정 기준을 쓰도록 한 곳에서 계산
        // 예전에는 궤적은 x < 0 → 0, 발사는 x < 10 → 10으로 보정해 짧게 당기면 궤적과 실제 발사가 달랐음
        private Vector2 CalculateLaunchForce(Vector2 dragVector)
        {
            if (dragVector.x < MinLaunchDragX)
            {
                dragVector.x = MinLaunchDragX;
            }

            Vector2 launchForce = dragVector * forceMultiplier;

            if (launchForce.magnitude > maxForce)
            {
                launchForce = launchForce.normalized * maxForce;
            }

            return launchForce;
        }

        private void SpawnBlock()
        {
            if (currentBlock != null) return;

            Block spawnBlock = GameManager.Instance.GetNextBlock();
            currentBlock = Instantiate(spawnBlock, spawnPoint);
            currentBlock.transform.localPosition = Vector3.zero;
        }

        private IEnumerator SpawnDelayCoroutine(Block dropBlock)
        {
            yield return new WaitForSeconds(0.5f);

            while (true)
            {
                if (GameManager.Instance.IsGameOver)
                {
                    yield break;
                }

                if (dropBlock == null || dropBlock.HasFirstContact || dropBlock.HasLost)
                {
                    break;
                }

                yield return null;
            }

            while (IsGameplayBlocked())
            {
                if (GameManager.Instance.IsGameOver)
                {
                    yield break;
                }

                yield return null;
            }

            if (UIManager.Instance.CurrentShip != null)
            {
                UIManager.Instance.SetCameraTarget(UIManager.Instance.CurrentShip.transform, UIManager.Instance.ShipPosOffset, 20f);
            }

            if (GameManager.Instance.IsGameOver)
            {
                yield break;
            }

            GameManager.Instance.ChangeWindForNextThrow();
            SpawnBlock();
        }

        private bool IsGameplayBlocked()
        {
            return StageFlowController.IsInitialized && StageFlowController.Instance.IsGameplayBlocked;
        }

        private void DrawTrajectory(Vector2 startPos, Vector2 velocity)
        {
            Vector3[] points = new Vector3[trajectoryPointCount];
            Vector2 gravity = Physics2D.gravity;
            float windPower = currentBlock != null ? currentBlock.WindPower : 0.0f;
            Vector2 windAcceleration = Vector2.right * GameManager.Instance.CurrentWindSpeed * windPower;

            for (int i = 0; i < trajectoryPointCount; i++)
            {
                float time = i * timeStepPerPoint;
                Vector2 totalAcceleration = gravity + windAcceleration;
                Vector2 pointPos = startPos + (velocity * time) + (0.5f * totalAcceleration * (time * time));
                points[i] = new Vector3(pointPos.x, pointPos.y, 0f);
            }

            trajectoryLine.SetPositions(points);
        }

        private Vector2 GetMouseWorldPosition()
        {
            Vector2 mouseScreenPos = Mouse.current.position.value;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -mainCamera.transform.position.z));
            return new Vector2(worldPos.x, worldPos.y);
        }
    }
}
