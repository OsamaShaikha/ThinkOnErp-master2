/**
 * Spike Load Test Script for Full Traceability System (50,000 req/min)
 * 
 * Purpose: Validate that the traceability system can handle sudden traffic spikes
 * up to 50,000 requests per minute and recover gracefully.
 * 
 * Requirements:
 * - System SHALL handle sudden bursts to 50,000 requests per minute
 * - Queue backpressure mechanisms SHALL prevent memory exhaustion
 * - System SHALL recover to normal performance after spike
 * - Error rate SHALL remain acceptable during spike (<5%)
 * - System SHALL NOT crash or become unresponsive
 * 
 * Prerequisites:
 * - k6 installed (https://k6.io/docs/getting-started/installation/)
 * - ThinkOnErp API running (default: http://localhost:5000)
 * - Valid JWT token for authentication
 * - Sufficient system resources (CPU, memory, database connections)
 * 
 * Usage:
 *   k6 run spike-load-test-50k-rpm.js
 *   k6 run --env API_URL=http://your-api-url spike-load-test-50k-rpm.js
 *   k6 run --out json=spike-results.json spike-load-test-50k-rpm.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

// Custom metrics for spike load validation
const auditOverhead = new Trend('audit_overhead_ms', true);
const apiResponseTime = new Trend('api_response_time_ms', true);
const errorRate = new Rate('error_rate');
const requestsPerMinute = new Counter('requests_per_minute');
const queueDepth = new Gauge('audit_queue_depth');
const memoryUsageMB = new Gauge('memory_usage_mb');

// Time-based metrics to track spike impact and recovery
const responseTimeBeforeSpike = new Trend('response_time_before_spike', true);
const responseTimeDuringSpike = new Trend('response_time_during_spike', true);
const responseTimeAfterSpike = new Trend('response_time_after_spike', true);

const errorRateBeforeSpike = new Rate('error_rate_before_spike');
const errorRateDuringSpike = new Rate('error_rate_during_spike');
const errorRateAfterSpike = new Rate('error_rate_after_spike');

// Test configuration
export const options = {
    // Spike load scenario: Sudden burst to 50,000 requests per minute
    scenarios: {
        spike_load_50k: {
            executor: 'ramping-arrival-rate',
            startRate: 1000,  // Start at baseline 1,000 requests per minute
            timeUnit: '1m',
            preAllocatedVUs: 200,
            maxVUs: 1000,  // Allow up to 1000 VUs for spike
            stages: [
                // Baseline phase (5 minutes at normal load)
                { duration: '5m', target: 10000 },  // Establish baseline at 10,000 req/min
                
                // Spike phase (sudden burst to 50,000 req/min)
                { duration: '30s', target: 50000 }, // SPIKE: Rapid increase to 50,000 req/min
                { duration: '3m', target: 50000 },  // Sustain spike for 3 minutes
                
                // Recovery phase (return to normal load)
                { duration: '1m', target: 10000 },  // Quick drop back to 10,000 req/min
                { duration: '5m', target: 10000 },  // Monitor recovery at normal load
                
                // Ramp down
                { duration: '2m', target: 0 },      // Graceful shutdown
            ],
        },
    },
    
    // Performance thresholds (adjusted for spike conditions)
    thresholds: {
        // During spike, we expect some degradation but system should remain functional
        'api_response_time_ms': [
            'p(99)<50',      // 99th percentile can be higher during spike (50ms acceptable)
            'p(95)<30',      // 95th percentile should stay reasonable
        ],
        
        // HTTP request duration (includes network + processing)
        'http_req_duration': [
            'p(99)<2000',    // 99% of requests should complete within 2 seconds (spike tolerance)
            'p(95)<1000',    // 95% of requests should complete within 1 second
            'avg<500',       // Average response time should be under 500ms
        ],
        
        // Error rate should be acceptable even during spike
        'error_rate': ['rate<0.05'],  // Less than 5% error rate (spike tolerance)
        
        // HTTP failures should be manageable
        'http_req_failed': ['rate<0.05'],
        
        // Recovery validation: After spike, performance should return to normal
        'response_time_after_spike': [
            'p(95)<300',     // After recovery, p95 should be back to normal (<300ms)
        ],
        
        'error_rate_after_spike': ['rate<0.01'],  // After recovery, error rate should be <1%
    },
};

// Environment configuration
const API_URL = __ENV.API_URL || 'http://localhost:5000';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

// Track test start time for phase detection
let testStartTime = 0;

// Test data generators
function generateCompanyData() {
    return {
        companyName: `SpikeTest Company ${Date.now()}`,
        companyNameSecondLanguage: `شركة اختبار ${Date.now()}`,
        address: '123 Test Street',
        addressSecondLanguage: 'شارع الاختبار 123',
        telephone: '+1234567890',
        mobile: '+0987654321',
        fax: '+1122334455',
        email: `spiketest${Date.now()}@example.com`,
        taxNumber: `TAX${Date.now()}`,
        commercialRegistrationNo: `CR${Date.now()}`,
        fiscalYearId: 1,
        defaultBranch: {
            branchName: `Main Branch ${Date.now()}`,
            branchNameSecondLanguage: `الفرع الرئيسي ${Date.now()}`,
            address: '123 Branch Street',
            addressSecondLanguage: 'شارع الفرع 123',
            telephone: '+1234567890',
            mobile: '+0987654321'
        }
    };
}

// Setup function - runs once per VU
export function setup() {
    console.log('=== Spike Load Test Setup (50,000 req/min) ===');
    console.log(`API URL: ${API_URL}`);
    console.log(`Baseline: 10,000 requests per minute`);
    console.log(`Spike Target: 50,000 requests per minute`);
    console.log(`Spike Duration: 3 minutes`);
    console.log(`Total Duration: ~17 minutes`);
    console.log('');
    console.log('Test Phases:');
    console.log('  1. Baseline (5 min at 10,000 req/min)');
    console.log('  2. Spike (3.5 min at 50,000 req/min)');
    console.log('  3. Recovery (5 min at 10,000 req/min)');
    console.log('  4. Ramp down (2 min)');
    console.log('==============================================');
    
    // Authenticate and get a valid token if not provided
    let token = JWT_TOKEN;
    
    if (!token) {
        console.log('No JWT token provided, attempting to authenticate...');
        const loginResponse = http.post(`${API_URL}/api/auth/login`, JSON.stringify({
            userName: 'superadmin',
            password: 'SuperAdmin123!'
        }), {
            headers: { 'Content-Type': 'application/json' },
        });
        
        if (loginResponse.status === 200) {
            const loginData = JSON.parse(loginResponse.body);
            token = loginData.token;
            console.log('Authentication successful');
        } else {
            console.error('Authentication failed:', loginResponse.status, loginResponse.body);
            throw new Error('Failed to authenticate. Please provide a valid JWT_TOKEN environment variable.');
        }
    }
    
    testStartTime = Date.now();
    
    return { token, testStartTime };
}

// Main test function - runs for each iteration
export default function(data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
    };
    
    // Calculate elapsed time since test start to determine phase
    const elapsedMinutes = (Date.now() - data.testStartTime) / 60000;
    
    // Determine current test phase
    let phase = 'baseline';
    if (elapsedMinutes >= 5 && elapsedMinutes < 8.5) {
        phase = 'spike';
    } else if (elapsedMinutes >= 8.5 && elapsedMinutes < 13.5) {
        phase = 'recovery';
    }
    
    // Mix of different API operations to simulate realistic load
    const operations = [
        testGetCompanies,
        testGetUsers,
        testGetRoles,
        testGetCurrencies,
        testGetBranches,
        testCreateCompany,
        testGetAuditLogs,
    ];
    
    // Randomly select an operation (weighted towards read operations)
    const weights = [30, 25, 15, 10, 10, 5, 5]; // Percentages
    const random = Math.random() * 100;
    let cumulative = 0;
    let selectedIndex = 0;
    
    for (let i = 0; i < weights.length; i++) {
        cumulative += weights[i];
        if (random < cumulative) {
            selectedIndex = i;
            break;
        }
    }
    
    // Execute the selected operation and track phase-specific metrics
    const result = operations[selectedIndex](headers);
    
    // Track metrics by phase
    if (phase === 'baseline') {
        responseTimeBeforeSpike.add(result.responseTime);
        errorRateBeforeSpike.add(result.error ? 1 : 0);
    } else if (phase === 'spike') {
        responseTimeDuringSpike.add(result.responseTime);
        errorRateDuringSpike.add(result.error ? 1 : 0);
    } else if (phase === 'recovery') {
        responseTimeAfterSpike.add(result.responseTime);
        errorRateAfterSpike.add(result.error ? 1 : 0);
    }
    
    // Increment requests per minute counter
    requestsPerMinute.add(1);
    
    // More frequent health checks during spike (every 50th request)
    if (phase === 'spike' && Math.random() < 0.02) {
        checkSystemHealth(headers);
    } else if (Math.random() < 0.01) {
        checkSystemHealth(headers);
    }
    
    // Minimal sleep during spike to maximize load
    if (phase === 'spike') {
        sleep(Math.random() * 0.01);  // 0-10ms
    } else {
        sleep(Math.random() * 0.1);   // 0-100ms
    }
}

// Test operations
function testGetCompanies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/companies`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/companies status is 200': (r) => r.status === 200,
        'GET /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    // Check for audit overhead (if response includes timing headers)
    if (response.headers['X-Audit-Overhead-Ms']) {
        auditOverhead.add(parseFloat(response.headers['X-Audit-Overhead-Ms']));
    }
    
    return { responseTime, error };
}

function testGetUsers(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/users`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/users status is 200': (r) => r.status === 200,
        'GET /api/users has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

function testGetRoles(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/roles`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/roles status is 200': (r) => r.status === 200,
        'GET /api/roles has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

function testGetCurrencies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/currencies`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/currencies status is 200': (r) => r.status === 200,
        'GET /api/currencies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

function testGetBranches(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/branches`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/branches status is 200': (r) => r.status === 200,
        'GET /api/branches has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

function testCreateCompany(headers) {
    const startTime = Date.now();
    const companyData = generateCompanyData();
    const response = http.post(
        `${API_URL}/api/companies`,
        JSON.stringify(companyData),
        { headers }
    );
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'POST /api/companies status is 200 or 201': (r) => r.status === 200 || r.status === 201,
        'POST /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

function testGetAuditLogs(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/auditlogs/legacy-view?pageNumber=1&pageSize=20`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/auditlogs status is 200': (r) => r.status === 200,
        'GET /api/auditlogs has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    const error = !success;
    if (error) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return { responseTime, error };
}

// Check system health metrics
function checkSystemHealth(headers) {
    const response = http.get(`${API_URL}/api/monitoring/health`, { headers });
    
    if (response.status === 200) {
        try {
            const healthData = JSON.parse(response.body);
            
            // Track queue depth if available
            if (healthData.auditQueueDepth !== undefined) {
                queueDepth.add(healthData.auditQueueDepth);
                
                // Log warnings if queue depth is high
                if (healthData.auditQueueDepth > 10000) {
                    console.warn(`WARNING: Audit queue depth exceeded 10,000: ${healthData.auditQueueDepth}`);
                }
                
                // Critical alert if queue depth is very high
                if (healthData.auditQueueDepth > 50000) {
                    console.error(`CRITICAL: Audit queue depth exceeded 50,000: ${healthData.auditQueueDepth}`);
                }
            }
            
            // Track memory usage if available
            if (healthData.memoryUsageMB !== undefined) {
                memoryUsageMB.add(healthData.memoryUsageMB);
            }
        } catch (e) {
            // Health endpoint might not return JSON or might not be available
            // This is optional monitoring, so we don't fail the test
        }
    }
}

// Teardown function - runs once after all VUs complete
export function teardown(data) {
    console.log('=== Spike Load Test Complete (50,000 req/min) ===');
    console.log('Test Duration: ~17 minutes');
    console.log('Spike Load: 50,000 req/min for 3 minutes');
    console.log('');
    console.log('Key metrics to review:');
    console.log('');
    console.log('1. SPIKE HANDLING:');
    console.log('   - response_time_during_spike (should be elevated but functional)');
    console.log('   - error_rate_during_spike (should be <5%)');
    console.log('   - audit_queue_depth (should not exceed memory limits)');
    console.log('');
    console.log('2. RECOVERY:');
    console.log('   - response_time_after_spike (should return to baseline)');
    console.log('   - error_rate_after_spike (should be <1%)');
    console.log('   - System should stabilize within 5 minutes');
    console.log('');
    console.log('3. BACKPRESSURE:');
    console.log('   - Queue depth should be managed (not grow unbounded)');
    console.log('   - Memory usage should remain stable');
    console.log('   - No system crashes or unresponsiveness');
    console.log('');
    console.log('Success Criteria:');
    console.log('  ✓ System handled spike without crashing');
    console.log('  ✓ Error rate during spike <5%');
    console.log('  ✓ System recovered to normal performance');
    console.log('  ✓ Queue backpressure prevented memory exhaustion');
    console.log('==================================================');
}
