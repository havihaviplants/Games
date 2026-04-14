using UnityEngine;
using System.Collections.Generic;

public class DynamicMover : MonoBehaviour
{
    // [변수 설정] 고수의 무빙 좌표 데이터와 이동 속도
    private List<Vector3> tacticData = new List<Vector3>();
    private int currentIndex = 0;
    public float moveSpeed = 5f;

    // 🛠️ 최적화 섹션: 게임이 켜질 때 딱 한 번 실행됨
    void Awake()
    {
        // 1. 720p 해상도 강제 집행 (내장 그래픽의 픽셀 연산 부담을 4배 줄임)
        Screen.SetResolution(1280, 720, false); 
        
        // 2. ★프레임 60 고정★ (GPU가 무한정 뺑뺑이 도는 걸 막아 팬 소음을 잠재움)
        Application.targetFrameRate = 60; 
        
        Debug.Log("아키텍트: 시스템 최적화 완료. 720p / 60FPS 모드로 가동합니다.");
    }

    // [데이터 주입] 노바 1492 고수의 '엇박자 무빙' 시뮬레이션
    void Start()
    {
        tacticData.Add(new Vector3(0, 0, 0));       // 시작점
        tacticData.Add(new Vector3(8, 0, 3));       // 1차 진입
        tacticData.Add(new Vector3(2, 0, 7));       // 엇박자 후퇴 (Juking)
        tacticData.Add(new Vector3(12, 0, 12));     // 기습 돌격
        tacticData.Add(new Vector3(-5, 0, 5));      // 측면 우회
        tacticData.Add(new Vector3(0, 0, 0));       // 복귀
    }

    // [추론 엔진] 실시간으로 좌표를 계산해 큐브를 이동시킴
    void Update()
    {
        if (tacticData.Count == 0) return;

        // 현재 목표로 삼은 좌표
        Vector3 targetPos = tacticData[currentIndex];

        // 부드럽게 목표 지점으로 이동 (Lerp 연산)
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveSpeed);

        // 목표 지점 근처(0.2f 거리)에 도착하면 다음 좌표로 전환
        if (Vector3.Distance(transform.position, targetPos) < 0.2f)
        {
            currentIndex = (currentIndex + 1) % tacticData.Count;
        }
    }
}