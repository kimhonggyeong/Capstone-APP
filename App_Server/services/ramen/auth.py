"""기존 모의투자 로그인 서버가 발급한 JWT를 검증한다."""
from fastapi import Header, HTTPException
from bson import ObjectId
from motor.motor_asyncio import AsyncIOMotorClient
import jwt

from services.ramen.config import settings

_auth_client: AsyncIOMotorClient | None = None


def _auth_database():
    global _auth_client
    uri = settings.auth_mongo_uri or settings.mongo_uri
    if _auth_client is None:
        _auth_client = AsyncIOMotorClient(uri)
    return _auth_client[settings.auth_mongo_db_name]


async def get_current_user_id(authorization: str | None = Header(default=None)) -> str:
    if not authorization:
        raise HTTPException(status_code=401, detail="로그인이 필요합니다.")

    scheme, separator, token = authorization.partition(" ")
    if separator != " " or scheme.lower() != "bearer" or not token:
        raise HTTPException(status_code=401, detail="올바른 Bearer 토큰이 필요합니다.")

    if not settings.jwt_secret:
        raise HTTPException(status_code=503, detail="AI 서버의 JWT_SECRET 설정이 필요합니다.")

    try:
        payload = jwt.decode(token, settings.jwt_secret, algorithms=["HS256"])
    except jwt.ExpiredSignatureError as error:
        raise HTTPException(status_code=401, detail="로그인이 만료되었습니다. 다시 로그인해 주세요.") from error
    except jwt.InvalidTokenError as error:
        raise HTTPException(status_code=401, detail="유효하지 않은 로그인 토큰입니다.") from error

    user_id = str(payload.get("id") or "").strip()
    if not ObjectId.is_valid(user_id):
        raise HTTPException(status_code=401, detail="로그인 사용자 정보가 올바르지 않습니다.")

    user = await _auth_database()["users"].find_one({"_id": ObjectId(user_id)}, {"_id": 1})
    if user is None:
        raise HTTPException(status_code=401, detail="사용자를 찾을 수 없습니다.")
    return user_id
