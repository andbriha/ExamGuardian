from flask import Flask, request, jsonify
import cv2
import numpy as np


app = Flask(__name__)

@app.route('/verify', methods=['POST'])
def verify_student():
    if 'photo' not in request.files:
        return jsonify({"error": "No photo provided"}), 400
    
    # 1. Отримуємо файл
    file = request.files['photo']
    img = cv2.imdecode(np.frombuffer(file.read(), np.uint8), cv2.IMREAD_COLOR)


    confidence = 0.95 

    return jsonify({
        "status": "success",
        "confidence": confidence,
        "is_verified": confidence > 0.7
    })

if __name__ == '__main__':
    app.run(port=5000)