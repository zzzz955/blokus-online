using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace Features.Multi.Net
{
    /// <summary>
    /// 네트워크 진단 및 연결성 테스트 도구
    /// 모바일에서 서버 연결 문제 진단용
    /// </summary>
    public static class NetworkDiagnostics
    {
        /// <summary>
        /// 서버 연결성 종합 테스트
        /// </summary>
        public static async Task<bool> DiagnoseConnection(string hostName, int port)
        {
            Debug.Log($"[NetworkDiagnostics] 서버 연결 진단 시작: {hostName}:{port}");
            
            // 1. 인터넷 연결 상태 확인
            bool internetCheck = CheckInternetConnection();
            Debug.Log($"[NetworkDiagnostics] 인터넷 연결: {internetCheck}");
            
            if (!internetCheck)
            {
                Debug.LogError("[NetworkDiagnostics] 인터넷 연결이 없습니다!");
                return false;
            }
            
            // 2. DNS 해결 테스트
            IPAddress[] addresses = await ResolveDNS(hostName);
            if (addresses == null || addresses.Length == 0)
            {
                Debug.LogError($"[NetworkDiagnostics] DNS 해결 실패: {hostName}");
                return false;
            }
            
            Debug.Log($"[NetworkDiagnostics] DNS 해결 성공: {hostName} -> {addresses[0]}");
            
            // 3. 핑 테스트
            bool pingResult = await TestPing(addresses[0].ToString());
            Debug.Log($"[NetworkDiagnostics] 핑 테스트: {pingResult}");
            
            // 4. 포트 연결 테스트 
            bool portResult = await TestPortConnection(addresses[0].ToString(), port);
            Debug.Log($"[NetworkDiagnostics] 포트 {port} 연결 테스트: {portResult}");
            
            return portResult;
        }
        
        /// <summary>
        /// 인터넷 연결 상태 확인
        /// </summary>
        private static bool CheckInternetConnection()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return false;
            }
            
            Debug.Log($"[NetworkDiagnostics] 네트워크 타입: {Application.internetReachability}");
            return true;
        }
        
        /// <summary>
        /// DNS 해결 (호스트명을 IP주소로 변환)
        /// </summary>
        private static async Task<IPAddress[]> ResolveDNS(string hostName)
        {
            try
            {
                // IP주소가 직접 입력된 경우
                if (IPAddress.TryParse(hostName, out IPAddress directIp))
                {
                    return new IPAddress[] { directIp };
                }
                
                // 호스트명 DNS 해결
                var hostEntry = await Dns.GetHostEntryAsync(hostName);
                return hostEntry.AddressList;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkDiagnostics] DNS 해결 오류: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 핑 테스트 (Unity 환경에서는 UnityEngine.Ping 사용)
        /// </summary>
        private static async Task<bool> TestPing(string ipAddress)
        {
            try
            {
                // Unity의 Ping 클래스 사용 (모바일 호환성 더 좋음)
                var ping = new UnityEngine.Ping(ipAddress);
                
                // 3초 대기
                float timeout = 3.0f;
                while (!ping.isDone && timeout > 0)
                {
                    await Task.Delay(100);
                    timeout -= 0.1f;
                }
                
                if (ping.isDone)
                {
                    Debug.Log($"[NetworkDiagnostics] 핑 시간: {ping.time}ms");
                    return ping.time >= 0; // 성공하면 양수 시간 반환
                }
                else
                {
                    Debug.LogWarning("[NetworkDiagnostics] 핑 타임아웃");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkDiagnostics] 핑 테스트 실패 (모바일에서 정상): {ex.Message}");
                return true; // 모바일에서는 핑이 막힐 수 있으므로 true 반환
            }
        }
        
        /// <summary>
        /// 포트 연결 테스트
        /// </summary>
        private static async Task<bool> TestPortConnection(string ipAddress, int port)
        {
            try
            {
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(10000); // 10초 타임아웃
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Debug.LogError($"[NetworkDiagnostics] 포트 연결 타임아웃: {ipAddress}:{port}");
                        return false;
                    }
                    
                    await connectTask;
                    Debug.Log($"[NetworkDiagnostics] 포트 연결 성공: {ipAddress}:{port}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkDiagnostics] 포트 연결 실패: {ex.Message}");
                return false;
            }
        }
    }
}