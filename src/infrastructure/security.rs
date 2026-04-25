use argon2::{ password_hash::{SaltString, PasswordHasher }, Argon2, };
use rand::rngs::OsRng;

pub fn hash_password(pass: &str) -> Result<String, String> {
    let salt = SaltString::generate(&mut OsRng);
    let argon2 = Argon2::default();
    let hash = argon2
        .hash_password(pass.as_bytes(), &salt)
        .map_err(|e| e.to_string())?
        .to_string();

    Ok(hash)
}
