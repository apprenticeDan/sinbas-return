use crate::domain::{user::User, user_error::UserError};
use crate::infrastructure::security::hash_password;

pub struct AuthService;

impl AuthService {
    pub fn register(id: u64, email: String, password: String,) -> Result<User, UserError> {
        let pass_hash = hash_password(&password)
            .map_err(|_| UserError::EmptyPassword)?;

        User::create(id, email, pass_hash)
    }
}

